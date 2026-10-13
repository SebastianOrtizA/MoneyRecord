using CommunityToolkit.Maui.Alerts;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>
    /// <see cref="IDialogService"/> on top of the page dialogs and the CommunityToolkit toast.
    /// </summary>
    public sealed class DialogService : IDialogService
    {
        // The window's page: AppShell, or the first-run setup before the Shell exists
        private static Page CurrentPage =>
            Application.Current?.Windows.FirstOrDefault()?.Page
            ?? throw new InvalidOperationException("No window is open.");

        public Task AlertAsync(string title, string message, string? close = null) =>
            MainThread.InvokeOnMainThreadAsync(() =>
                CurrentPage.DisplayAlertAsync(title, message, close ?? AppResources.OK));

        public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
            MainThread.InvokeOnMainThreadAsync(() =>
                CurrentPage.DisplayAlertAsync(title, message, accept, cancel));

        public Task<string?> PromptAsync(string title, string message, string accept, string cancel,
            string? initialValue = null, bool numeric = false) =>
            MainThread.InvokeOnMainThreadAsync<string?>(() =>
                CurrentPage.DisplayPromptAsync(title, message, accept, cancel,
                    initialValue: initialValue ?? string.Empty,
                    keyboard: numeric ? Keyboard.Numeric : Keyboard.Default));

        public async Task<string?> ActionSheetAsync(string title, string cancel, IReadOnlyList<string> options)
        {
            var choice = await MainThread.InvokeOnMainThreadAsync(() =>
                CurrentPage.DisplayActionSheetAsync(title, cancel, null, options.ToArray()));

            return choice == null || choice == cancel ? null : choice;
        }

        public Task ToastAsync(string message) =>
            MainThread.InvokeOnMainThreadAsync(() => Toast.Make(message).Show());
    }
}
