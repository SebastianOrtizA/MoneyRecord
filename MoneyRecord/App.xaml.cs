using Microsoft.Extensions.Logging;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Views;

namespace MoneyRecord
{
    public partial class App : Application
    {
        private readonly IRecurringTransactionService _recurringTransactionService;
        private readonly ISetupService _setupService;
        private readonly Func<SetupPage> _createSetupPage;
        private readonly AppLock _appLock;
        private readonly IDeviceLock _deviceLock;
        private readonly IPrivacySettings _privacy;
        private readonly Func<LockPage> _createLockPage;
        private readonly ILogger<App> _logger;

        public App(IRecurringTransactionService recurringTransactionService, ISetupService setupService,
            Func<SetupPage> createSetupPage, IThemeService themeService, AppLock appLock, IDeviceLock deviceLock,
            IPrivacySettings privacy, Func<LockPage> createLockPage, ILogger<App> logger)
        {
            InitializeComponent();
            // Before the first page is created, so it never flashes in the other theme
            themeService.ApplySaved();
            _recurringTransactionService = recurringTransactionService;
            _setupService = setupService;
            _createSetupPage = createSetupPage;
            _appLock = appLock;
            _deviceLock = deviceLock;
            _privacy = privacy;
            _createLockPage = createLockPage;
            _logger = logger;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // A new install starts with the setup; it swaps itself for the Shell when finished
            Page page = _setupService.ResolveFirstRunPending()
                ? _createSetupPage().AsFirstRun()
                : new AppShell();
            var window = new Window(page);
            var lockOnStart = _appLock.LockOnStart();
            // Nothing of the app is drawn until the lock screen covers it (no frame of balances before it)
            SetContentHidden(window, lockOnStart);

            window.Created += (s, e) =>
            {
                _ = ProcessRecurringTransactionsAsync();
#if ANDROID
                // While the app is closed, the daily job creates them (and sends the reminders)
                Platforms.Android.DailyReminderWorker.Schedule(Android.App.Application.Context);
#endif
                _deviceLock.HideContentInRecents(_privacy.LockEnabled);
                if (lockOnStart)
                    ShowLock(window);
            };

            // The app lock: away for longer than the chosen delay locks it again. Until coming back decides that, the
            // app's content stays hidden, so its first frame can't show the balances the lock screen is about to cover
            window.Stopped += (s, e) =>
            {
                _appLock.Backgrounded();
                if (_privacy.LockEnabled)
                    SetContentHidden(window, true);
            };
            // Activated, not Resumed: on Android Resumed comes before the activity is in front, where the phone's lock
            // prompt would be cancelled. Every activation asks; only a return from the background can lock
            window.Activated += (s, e) =>
            {
                if (_appLock.LockOnResume())
                    ShowLock(window);
                else
                    SetContentHidden(window, false);
            };

            return window;
        }

        private static void SetContentHidden(Window window, bool hidden)
        {
            if (window.Page is { } page)
                page.Opacity = hidden ? 0 : 1;
        }

        /// <summary>
        /// Covers the app with the lock screen (once: a second call while it is up does nothing), then shows the content
        /// under it again.
        /// </summary>
        private void ShowLock(Window window) => MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var navigation = window.Page?.Navigation;
                if (navigation != null && navigation.ModalStack.LastOrDefault() is not LockPage)
                    await navigation.PushModalAsync(_createLockPage(), animated: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show the lock screen");
            }
            finally
            {
                // Also after a failure: a hidden app that never comes back would be worse
                SetContentHidden(window, false);
            }
        });

        private async Task ProcessRecurringTransactionsAsync()
        {
            try
            {
                await _recurringTransactionService.ProcessDueTransactionsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process recurring transactions");
            }
        }
    }
}
