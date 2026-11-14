using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    /// <summary>
    /// A modal page for choosing the app PIN. <see cref="Result"/> completes with true once the PIN is saved, or false when
    /// cancelled (Cancel or Android back).
    /// </summary>
    public partial class PinSetupPage : ContentPage
    {
        private readonly TaskCompletionSource<bool> _result = new();

        public PinSetupPage(PinSetupViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
            viewModel.Finished += OnFinished;
        }

        public Task<bool> Result => _result.Task;

        /// <summary>On Android a modal page gets back before the Shell does: back cancels.</summary>
        protected override bool OnBackButtonPressed()
        {
            ((PinSetupViewModel)BindingContext).CancelCommand.Execute(null);
            return true;
        }

        private async void OnFinished(bool saved)
        {
            if (_result.Task.IsCompleted)
                return;
            await Navigation.PopModalAsync(animated: false);
            _result.TrySetResult(saved);
        }
    }
}
