using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Views
{
    /// <summary>
    /// The app lock screen. It asks for the phone's lock as soon as it appears, and again from its button after a cancel.
    /// Android back leaves the app.
    /// </summary>
    public partial class LockPage : ContentPage
    {
        private readonly AppLock _appLock;
        private readonly IDeviceLock _deviceLock;
        private bool _asking;

        public LockPage(AppLock appLock, IDeviceLock deviceLock)
        {
            InitializeComponent();
            _appLock = appLock;
            _deviceLock = deviceLock;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await UnlockAsync();
        }

        private async void OnUnlockClicked(object? sender, EventArgs e) => await UnlockAsync();

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

        private async Task UnlockAsync()
        {
            if (_asking)
                return;

            _asking = true;
            try
            {
                // The phone's screen lock was removed meanwhile: nothing left to ask for (AppLock stops locking too)
                if (!_deviceLock.IsAvailable
                    || await _deviceLock.AuthenticateAsync(AppResources.AppLocked, AppResources.UnlockSubtitle))
                {
                    _appLock.Unlock();
                    await Navigation.PopModalAsync(animated: false);
                }
            }
            finally
            {
                _asking = false;
            }
        }
    }
}
