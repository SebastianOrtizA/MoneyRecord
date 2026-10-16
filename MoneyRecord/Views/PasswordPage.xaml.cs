using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Views
{
    /// <summary>
    /// A modal page asking for a password (typed hidden, "Show password" to check it). With confirm it is typed twice.
    /// <see cref="Result"/> completes with the password, or null when cancelled (Cancel or Android back).
    /// </summary>
    public partial class PasswordPage : ContentPage
    {
        private readonly bool _confirm;
        private readonly TaskCompletionSource<string?> _result = new();

        public PasswordPage(string title, string message, bool confirm)
        {
            InitializeComponent();
            _confirm = confirm;
            TitleLabel.Text = title;
            MessageLabel.Text = message;
            RepeatEntry.IsVisible = confirm;
            PasswordEntry.ReturnType = confirm ? ReturnType.Next : ReturnType.Done;
        }

        public Task<string?> Result => _result.Task;

        protected override void OnAppearing()
        {
            base.OnAppearing();
            // After the page is shown, so the keyboard opens with it
            Dispatcher.Dispatch(() => PasswordEntry.Focus());
        }

        private void OnShowChanged(object? sender, CheckedChangedEventArgs e)
        {
            PasswordEntry.IsPassword = !e.Value;
            RepeatEntry.IsPassword = !e.Value;
        }

        private void OnShowLabelTapped(object? sender, TappedEventArgs e) => ShowCheckBox.IsChecked = !ShowCheckBox.IsChecked;

        private void OnPasswordCompleted(object? sender, EventArgs e)
        {
            if (_confirm)
                RepeatEntry.Focus();
            else
                OnOkClicked(sender, e);
        }

        private async void OnOkClicked(object? sender, EventArgs e)
        {
            var password = PasswordEntry.Text ?? string.Empty;
            var error = password.Length == 0 ? AppResources.PasswordEmpty
                : _confirm && password != RepeatEntry.Text ? AppResources.PasswordsDontMatch
                : null;
            ErrorLabel.Text = error;
            ErrorLabel.IsVisible = error != null;
            if (error == null)
                await CloseAsync(password);
        }

        private async void OnCancelClicked(object? sender, EventArgs e) => await CloseAsync(null);

        /// <summary>On Android a modal page gets back before the Shell does: back cancels.</summary>
        protected override bool OnBackButtonPressed()
        {
            _ = CloseAsync(null);
            return true;
        }

        private async Task CloseAsync(string? password)
        {
            if (_result.Task.IsCompleted)
                return;
            PasswordEntry.Unfocus();
            RepeatEntry.Unfocus();
            await Navigation.PopModalAsync(animated: false);
            _result.TrySetResult(password);
        }
    }
}
