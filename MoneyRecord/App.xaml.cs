using Microsoft.Extensions.Logging;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Sync;
using MoneyRecord.Views;

namespace MoneyRecord
{
    public partial class App : Application
    {
        private readonly IReminderService _reminders;
        private readonly IExchangeRateService _exchangeRates;
        private readonly IAutoBackupService _autoBackup;
        private readonly ISetupService _setupService;
        private readonly Func<SetupPage> _createSetupPage;
        private readonly AppLock _appLock;
        private readonly IDeviceLock _deviceLock;
        private readonly IPrivacySettings _privacy;
        private readonly IScreenProtection _screen;
        private readonly Func<LockPage> _createLockPage;
        private readonly IHomeScreenWidgets _widgets;
        private readonly IWhatsNewService _whatsNew;
        private readonly ITipJar _tipJar;
        private readonly NotificationLaunch _launch;
        private readonly IReceiptStore _receipts;
        private readonly ISyncService _sync;
        private readonly ILogger<App> _logger;

        public App(IReminderService reminders, ISetupService setupService,
            Func<SetupPage> createSetupPage, IThemeService themeService, AppLock appLock, IDeviceLock deviceLock,
            IPrivacySettings privacy, IScreenProtection screen, Func<LockPage> createLockPage, IExchangeRateService exchangeRates, IAutoBackupService autoBackup,
            IHomeScreenWidgets widgets, IWhatsNewService whatsNew, ITipJar tipJar, NotificationLaunch launch, IReceiptStore receipts, ISyncService sync, ILogger<App> logger)
        {
            InitializeComponent();
            // Before the first page is created, so it never flashes in the other theme
            themeService.ApplySaved();
            _reminders = reminders;
            _exchangeRates = exchangeRates;
            _autoBackup = autoBackup;
            _setupService = setupService;
            _createSetupPage = createSetupPage;
            _appLock = appLock;
            _deviceLock = deviceLock;
            _privacy = privacy;
            _screen = screen;
            _createLockPage = createLockPage;
            _widgets = widgets;
            _whatsNew = whatsNew;
            _tipJar = tipJar;
            _launch = launch;
            _receipts = receipts;
            _sync = sync;
            // Decrypted copies handed to a PDF viewer last time
            receipts.ClearViewFolder();
            _logger = logger;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // A new install starts with the setup; it swaps itself for the Shell when finished
            var firstRun = _setupService.ResolveFirstRunPending();
            // Nothing new to announce to a new install: it starts at this version
            if (firstRun)
                _whatsNew.SkipForNewInstall();
            Page page = firstRun
                ? _createSetupPage().AsFirstRun()
                : new AppShell();
            var window = new Window(page);
            var lockOnStart = _appLock.LockOnStart();
            // Nothing of the app is drawn until the lock screen covers it (no frame of balances before it)
            SetContentHidden(window, lockOnStart);

            window.Created += (s, e) =>
            {
#if WINDOWS
                Platforms.Windows.WindowsDesktop.Configure(window);
                // No background job on Windows: the same steps run now and every hour while the app is open
                Platforms.Windows.WindowsDailyJob.Start(_reminders, _exchangeRates, _autoBackup, _logger);
                // Listens for the phone again when sync is on (the computer is the side that waits)
                _ = ResumeSyncAsync();
                Platforms.Windows.WindowsSyncTimer.Start(_sync, _logger);
#else
                _ = ProcessRecurringThenBackUpAsync();
#endif
                // Opt-in, once a day, best-effort: nothing waits for it
                _ = _exchangeRates.UpdateIfDueAsync();
                // A tip whose payment settled after the app closed (Play build; nothing elsewhere): best-effort, logs its own failures
                _ = _tipJar.ConsumePendingAsync();
#if ANDROID
                // While the app is closed, the daily job creates them (and sends the reminders)
                Platforms.Android.DailyReminderWorker.Schedule(Android.App.Application.Context);
                // A paired phone syncs in the background too (nothing is scheduled while sync is off)
                _ = ResumeSyncAsync();
#endif
                _deviceLock.HideContentInRecents(_privacy.LockEnabled);
                ApplyScreenProtection();
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
                // Whatever changed while the app was open (entries, accounts, Hide balances, the language) shows on the
                // home screen it is going back to
                _ = _widgets.RefreshAsync();
            };
            // Activated, not Resumed: on Android Resumed comes before the activity is in front, where the phone's lock
            // prompt would be cancelled. Every activation asks; only a return from the background can lock
            window.Activated += (s, e) =>
            {
                var cameBack = _appLock.IsAway;
                var locks = _appLock.LockOnResume();
                ApplyScreenProtection();
                if (locks)
                {
                    // The lock screen opens a page requested meanwhile once it is unlocked
                    ShowLock(window);
                }
                else
                {
                    SetContentHidden(window, false);
                    // Android hands a tapped notification, widget button or shortcut over before this decision (a cold start
                    // opens it from Home instead, and not without a Shell: the first-run setup)
                    if (cameBack)
                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            if (Shell.Current != null)
                                await _launch.OpenPendingAsync();
                        });
                }
            };

            return window;
        }

        /// <summary>The lock screen is always hidden from screenshots; the rest follows the setting. Also after the activity was recreated.</summary>
        private void ApplyScreenProtection() => _screen.SetSecure(_privacy.BlockScreenshots || _appLock.IsLocked);

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
            _receipts.ClearViewFolder(); // nothing decrypted stays behind a lock
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

        private async Task ResumeSyncAsync()
        {
            try
            {
                await _sync.ResumeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Resuming sync failed");
            }
        }

        private async Task ProcessRecurringThenBackUpAsync()
        {
            try
            {
                // Also notifies of the debts its scheduled payments paid off
                await _reminders.ProcessRecurringAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process recurring transactions");
            }
            // When due (best-effort, logs its own failures); after processing, so the backup has the new transactions
            await _autoBackup.RunIfDueAsync();
        }
    }
}
