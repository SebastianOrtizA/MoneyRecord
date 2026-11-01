using MoneyRecord.ViewModels;

namespace MoneyRecord.Controls
{
    public partial class AddEntryButton : ContentView
    {
        private readonly AddEntryViewModel _viewModel;

        public AddEntryButton()
        {
            InitializeComponent();
            _viewModel = IPlatformApplication.Current!.Services.GetRequiredService<AddEntryViewModel>();
            BindingContext = _viewModel;
#if ANDROID
            // Long-press goes straight to a new expense. The native listener: MAUI's own touch listener on a Button would
            // replace a toolkit TouchBehavior's, so that never fires. The tap still opens the sheet
            AddButton.HandlerChanged += (_, _) =>
            {
                if (AddButton.Handler?.PlatformView is global::Android.Views.View view)
                {
                    view.LongClickable = true;
                    view.LongClick += (_, e) =>
                    {
                        view.PerformHapticFeedback(global::Android.Views.FeedbackConstants.LongPress);
                        _viewModel.AddExpenseNowCommand.Execute(null);
                        e.Handled = true;
                    };
                }
            };
#endif
        }

        /// <summary>True when the sheet was open and is now closed: lets a page's back button close it first.</summary>
        public bool TryClose()
        {
            if (!_viewModel.IsOpen)
                return false;

            _viewModel.IsOpen = false;
            return true;
        }
    }
}
