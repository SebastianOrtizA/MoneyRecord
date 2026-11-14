using MoneyRecord.Services;
using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    /// <summary>
    /// The app lock screen: the PIN pad and/or the phone's lock (asked as soon as the screen appears, and again from its
    /// button after a cancel). Android back leaves the app.
    /// </summary>
    public partial class LockPage : ContentPage
    {
        private readonly LockViewModel _viewModel;
        private readonly NotificationLaunch _launch;
        private readonly IDispatcherTimer _timer;
        private bool _closing;

        public LockPage(LockViewModel viewModel, NotificationLaunch launch)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _launch = launch;
            BindingContext = viewModel;
            viewModel.Unlocked += OnUnlocked;
            // Counts the lockout wait down
            _timer = Dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (_, _) => _viewModel.Tick();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            _timer.Start();
            await _viewModel.InitializeAsync();
        }

        protected override void OnDisappearing()
        {
            _timer.Stop();
            base.OnDisappearing();
        }

        /// <summary>
        /// Back must never close the lock screen (a modal page closes on back by default): it leaves the app instead.
        /// On Android a modal page gets back before the Shell does.
        /// </summary>
        protected override bool OnBackButtonPressed()
        {
#if ANDROID
            Platform.CurrentActivity?.MoveTaskToBack(true);
#endif
            return true;
        }

        private async void OnUnlocked()
        {
            if (_closing)
                return;
            _closing = true;
            _viewModel.Unlocked -= OnUnlocked;
            await Navigation.PopModalAsync(animated: false);
            // A shortcut, widget button or notification that started the app waited for this unlock (never throws).
            // Not without a Shell (the first-run setup): Home's first visit opens it
            if (Shell.Current != null)
                await _launch.OpenPendingAsync();
        }
    }
}
