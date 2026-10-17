using CommunityToolkit.Maui;
using LiveChartsCore.SkiaSharpView.Maui;
using Microsoft.Extensions.Logging;
using MoneyRecord.Helpers;
using System.Reflection;
using Serilog;
using Serilog.Events;
using SkiaSharp.Views.Maui.Controls.Hosting;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;
using MoneyRecord.ViewModels;
using MoneyRecord.Views;

namespace MoneyRecord
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            // The saved language (or the device's), and the base currency, before anything formats money or opens the
            // database (migration 3 stamps old accounts with the currency)
            var preferences = new PreferencesService();
            LocalizationService.Instance.LoadLanguage(preferences);
            CurrencyService.LoadSettings(preferences);
            var paths = new AppPaths(FileSystem.AppDataDirectory);
            var sentryDsn = SentryDsn();
            var crashReports = new CrashReportConsent(preferences, TimeProvider.System, isAvailable: sentryDsn != null);

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("materialdesignicons-webfont.ttf", "MaterialDesignIcons");
                })
                // Toolkit converters return a default instead of throwing on unexpected input, like ours did
                .UseMauiCommunityToolkit(options => options.SetShouldSuppressExceptionsInConverters(true))
                .ConfigureDecimalEntry()
                .ConfigureTabReselect()
                // Reports charts (LiveChartsCore on SkiaSharp)
                .UseSkiaSharp()
                .UseLiveCharts()
                .ConfigureLogging(paths.LogsFolder)
                .ConfigureCrashReports(sentryDsn, crashReports);

            // Register Infrastructure Services
            builder.Services.AddSingleton<LocalizationService>(_ => LocalizationService.Instance);
            builder.Services.AddSingleton<INavigationService, NavigationService>();
            builder.Services.AddSingleton<IDialogService, DialogService>();
            builder.Services.AddSingleton<IPreferencesService, PreferencesService>();
            builder.Services.AddSingleton<ICategoryIconService, CategoryIconService>();
            builder.Services.AddSingleton<ICurrencyService, CurrencyService>();
            builder.Services.AddSingleton<IThemeService, ThemeService>();
            builder.Services.AddSingleton<ICrashReportConsent>(crashReports);
            builder.Services.AddSingleton(TimeProvider.System);

            // Register Database Infrastructure
            builder.Services.AddSingleton<IAppPaths>(paths);
            builder.Services.AddSingleton<DatabaseInitializer>();
            // Optional encryption (Settings): the key in SecureStorage, Android only for now like the app lock
            builder.Services.AddSingleton<IDatabaseEncryption>(sp => sp.GetRequiredService<DatabaseInitializer>());
#if ANDROID
            builder.Services.AddSingleton<IDatabaseKeyStore, SecureDatabaseKeyStore>();
#else
            builder.Services.AddSingleton<IDatabaseKeyStore>(NoDatabaseKeyStore.Instance);
#endif

            // Register Repositories (Interface Segregation)
            builder.Services.AddSingleton<ICategoryRepository, CategoryRepository>();
            builder.Services.AddSingleton<IAccountRepository, AccountRepository>();
            builder.Services.AddSingleton<ITransactionRepository, TransactionRepository>();
            builder.Services.AddSingleton<ITransferRepository, TransferRepository>();
            builder.Services.AddSingleton<IBudgetRepository, BudgetRepository>();
            builder.Services.AddSingleton<IRecurringTransactionRepository, RecurringTransactionRepository>();
            builder.Services.AddSingleton<IExchangeRateRepository, ExchangeRateRepository>();
            builder.Services.AddSingleton<IPayeeRepository, PayeeRepository>();

            // Register Business Services
            builder.Services.AddSingleton<IBalanceService, BalanceService>();
            builder.Services.AddSingleton<ITransactionEnrichmentService, TransactionEnrichmentService>();
            builder.Services.AddSingleton<ILedgerService, LedgerService>();
            builder.Services.AddSingleton<IBudgetSummaryService, BudgetSummaryService>();
            builder.Services.AddSingleton<IRecurringTransactionService, RecurringTransactionService>();
            builder.Services.AddSingleton<IErrorHandler, ErrorHandler>();
            builder.Services.AddSingleton<IDataExportService, DataExportService>();
            builder.Services.AddSingleton<IReportExportService, ReportExportService>();
            builder.Services.AddSingleton<IFileSharer, FileSharer>();
            builder.Services.AddSingleton<IStatementImportService, StatementImportService>();
            builder.Services.AddSingleton<IStatementMappingStore, StatementMappingStore>();
            builder.Services.AddSingleton<IAccountService, AccountService>();
            builder.Services.AddSingleton<ISavingsGoalService, SavingsGoalService>();
            builder.Services.AddSingleton<IPayeeService, PayeeService>();
            builder.Services.AddSingleton<IDebtService, DebtService>();
            builder.Services.AddSingleton<ISplitService, SplitService>();
            builder.Services.AddSingleton<ITripService, TripService>();
            builder.Services.AddSingleton<IPartyService, PartyService>();
            builder.Services.AddSingleton<ICategoryService, CategoryService>();
            builder.Services.AddSingleton<IEntryDefaultsService, EntryDefaultsService>();
            builder.Services.AddSingleton<ISetupService, SetupService>();
            builder.Services.AddSingleton<IExchangeRateSettings, ExchangeRateSettings>();
            builder.Services.AddSingleton<IExchangeRateSource>(_ => new FrankfurterRateSource(
                new HttpClient { BaseAddress = FrankfurterRateSource.BaseAddress, Timeout = TimeSpan.FromSeconds(30) }));
            builder.Services.AddSingleton<IExchangeRateService, ExchangeRateService>();
            builder.Services.AddSingleton<ICurrencyDisplaySettings, CurrencyDisplaySettings>();
            builder.Services.AddSingleton<IConversionService, ConversionService>();
            // A report asks the Transactions tab to open filtered
            builder.Services.AddSingleton<TransactionsDrillDown>();

            // Reminders: Android notifications and a daily background job (Platforms/Android/DailyReminderWorker)
            builder.Services.AddSingleton<IReminderSettings, ReminderSettings>();
            builder.Services.AddSingleton<IReminderService, ReminderService>();
            builder.Services.AddSingleton<NotificationLaunch>();
#if ANDROID
            builder.Services.AddSingleton<INotificationService, Platforms.Android.AndroidNotificationService>();
#else
            builder.Services.AddSingleton<INotificationService, NoNotificationService>();
#endif

            // Automatic backups into a folder the user picks (Android: the system folder picker), from the daily job
            builder.Services.AddSingleton<IAutoBackupSettings, AutoBackupSettings>();
            builder.Services.AddSingleton<IAutoBackupService, AutoBackupService>();
#if ANDROID
            builder.Services.AddSingleton<IBackupFolder, Platforms.Android.AndroidBackupFolder>();
#else
            builder.Services.AddSingleton<IBackupFolder, NoBackupFolder>();
#endif
            builder.Services.AddTransient<AutoBackupViewModel>();
            // The backup password (manual and automatic backups), in secure storage: Android only, like the database key
#if ANDROID
            builder.Services.AddSingleton<IBackupPasswordStore, SecureBackupPasswordStore>();
#else
            builder.Services.AddSingleton<IBackupPasswordStore, NoBackupPasswordStore>();
#endif
            builder.Services.AddTransient<BackupPasswordViewModel>();
            builder.Services.AddTransient<CurrencyCentsViewModel>();

            // App lock (the phone's own lock) and hidden balances
            builder.Services.AddSingleton<IPrivacySettings, PrivacySettings>();
            builder.Services.AddSingleton<AppLock>();
#if ANDROID
            builder.Services.AddSingleton<IDeviceLock, Platforms.Android.AndroidDeviceLock>();
#else
            builder.Services.AddSingleton<IDeviceLock, NoDeviceLock>();
#endif

            // The home-screen widget (Android: Platforms/Android/BalanceWidgetProvider), redrawn when the app goes to the
            // background and after the daily job
            builder.Services.AddSingleton<IBalanceWidget, BalanceWidget>();
#if ANDROID
            builder.Services.AddSingleton<IHomeScreenWidgets, Platforms.Android.AndroidHomeScreenWidgets>();
#else
            builder.Services.AddSingleton<IHomeScreenWidgets, NoHomeScreenWidgets>();
#endif

            // Receipt photos: the total and date read on the phone (Android: ML Kit)
#if ANDROID
            builder.Services.AddSingleton<IReceiptScanner, Platforms.Android.AndroidReceiptScanner>();
#else
            builder.Services.AddSingleton<IReceiptScanner, NoReceiptScanner>();
#endif

            // Register ViewModels
            // The one period every tab shows
            builder.Services.AddSingleton<PeriodViewModel>();
            builder.Services.AddSingleton<MainViewModel>();
            builder.Services.AddTransient<TransactionsViewModel>();
            builder.Services.AddTransient<MoreViewModel>();
            builder.Services.AddTransient<AddTransactionViewModel>();
            builder.Services.AddTransient<ManageCategoriesViewModel>();
            builder.Services.AddTransient<ManageAccountsViewModel>();
            builder.Services.AddTransient<TransfersViewModel>();
            builder.Services.AddTransient<AddTransferViewModel>();
            builder.Services.AddTransient<CategoryReportViewModel>();
            builder.Services.AddTransient<ReportExportViewModel>();
            builder.Services.AddTransient<AddEntryViewModel>();
            builder.Services.AddTransient<BudgetViewModel>();
            builder.Services.AddTransient<GoalsViewModel>();
            builder.Services.AddTransient<DebtsViewModel>();
            builder.Services.AddTransient<TripsViewModel>();
            builder.Services.AddTransient<TripViewModel>();
            builder.Services.AddTransient<TripCategoriesViewModel>();
            builder.Services.AddTransient<PartiesViewModel>();
            builder.Services.AddTransient<PartyViewModel>();
            builder.Services.AddTransient<FriendsViewModel>();
            builder.Services.AddTransient<PayeesViewModel>();
            builder.Services.AddTransient<RecurringTransactionsViewModel>();
            builder.Services.AddTransient<SettingsViewModel>();
            builder.Services.AddTransient<SetupViewModel>();
            builder.Services.AddTransient<AboutViewModel>();
            builder.Services.AddTransient<LicenseViewModel>();
            builder.Services.AddTransient<ImportStatementViewModel>();
            builder.Services.AddTransient<TrendsReportViewModel>();
            builder.Services.AddTransient<CashFlowReportViewModel>();
            builder.Services.AddTransient<NetWorthReportViewModel>();

            // Register Views
            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddTransient<TransactionsPage>();
            builder.Services.AddTransient<MorePage>();
            builder.Services.AddTransient<AddTransactionPage>();
            builder.Services.AddTransient<ManageCategoriesPage>();
            builder.Services.AddTransient<ManageAccountsPage>();
            builder.Services.AddTransient<TransfersPage>();
            builder.Services.AddTransient<AddTransferPage>();
            builder.Services.AddTransient<ExpenseReportPage>();
            builder.Services.AddTransient<IncomeReportPage>();
            builder.Services.AddTransient<BudgetPage>();
            builder.Services.AddTransient<GoalsPage>();
            builder.Services.AddTransient<DebtsPage>();
            builder.Services.AddTransient<TripsPage>();
            builder.Services.AddTransient<TripPage>();
            builder.Services.AddTransient<TripCategoriesPage>();
            builder.Services.AddTransient<PartiesPage>();
            builder.Services.AddTransient<PartyPage>();
            builder.Services.AddTransient<FriendsPage>();
            builder.Services.AddTransient<PayeesPage>();
            builder.Services.AddTransient<RecurringTransactionsPage>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<SetupPage>();
            builder.Services.AddTransient<AboutPage>();
            builder.Services.AddTransient<LicensePage>();
            builder.Services.AddTransient<ImportStatementPage>();
            // App creates it only on a new install
            builder.Services.AddTransient<Func<SetupPage>>(services => () => services.GetRequiredService<SetupPage>());
            builder.Services.AddTransient<LockPage>();
            builder.Services.AddTransient<Func<LockPage>>(services => () => services.GetRequiredService<LockPage>());
            builder.Services.AddTransient<TrendsReportPage>();
            builder.Services.AddTransient<CashFlowReportPage>();
            builder.Services.AddTransient<NetWorthReportPage>();

            return builder.Build();
        }

        /// <summary>
        /// Every <see cref="ILogger{T}"/> writes to a daily file in the app's logs folder (shared from Settings), and to
        /// the debugger in Debug builds. A file rolls over at 1 MB; files older than <see cref="LogFiles.RetainedDays"/> days
        /// and all but the newest <see cref="LogFiles.MaxFiles"/> are deleted, so an error loop can't fill the device and
        /// the newest events (such as the crash that ends it) are kept. Crashes are logged before the process ends.
        /// </summary>
        private static MauiAppBuilder ConfigureLogging(this MauiAppBuilder builder, string logsFolder)
        {
            Log.Logger = new LoggerConfiguration()
#if DEBUG
                .MinimumLevel.Debug()
#else
                .MinimumLevel.Information()
#endif
                // MAUI's own chatter; its warnings and errors still get through
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .WriteTo.File(LogFiles.RollingPath(logsFolder),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: LogFiles.MaxFiles,
                    retainedFileTimeLimit: TimeSpan.FromDays(LogFiles.RetainedDays),
                    fileSizeLimitBytes: LogFiles.MaxFileBytes,
                    rollOnFileSizeLimit: true)
                .CreateLogger();

            builder.Logging.AddSerilog(dispose: true);
#if DEBUG
            builder.Logging.AddDebug();
#endif

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception");
                Log.CloseAndFlush();
            };
            TaskScheduler.UnobservedTaskException += (_, e) => Log.Error(e.Exception, "Unobserved task exception");

            Log.Information("MoneyRecord {Version} ({Build}) on {Platform} {OsVersion}",
                AppInfo.Current.VersionString, AppInfo.Current.BuildString,
                DeviceInfo.Current.Platform, DeviceInfo.Current.VersionString);
            return builder;
        }

        /// <summary>
        /// Sentry crash reports, only when this build has a DSN (CI builds) and the user agreed. Consent is checked at
        /// start; turning it off in Settings stops sending at once (BeforeSend, for .NET and Android SDK events alike).
        /// Errors logged through ILogger are sent too. Nothing that identifies the user or shows their data: no
        /// screenshots (screens show amounts), no default PII, no UI text in breadcrumbs, no session tracking.
        /// </summary>
        private static MauiAppBuilder ConfigureCrashReports(this MauiAppBuilder builder, string? dsn,
            ICrashReportConsent consent)
        {
            if (dsn == null || !consent.Enabled)
                return builder;

            return builder.UseSentry(options =>
            {
                options.Dsn = dsn;
                options.Release = $"moneyrecord@{AppInfo.Current.VersionString}+{AppInfo.Current.BuildString}";
#if DEBUG
                options.Environment = "debug";
#endif
                options.SendDefaultPii = false;
                options.AttachScreenshot = false;
                options.IncludeTextInBreadcrumbs = false;
                options.IncludeTitleInBreadcrumbs = false;
                options.AutoSessionTracking = false;
                options.SetBeforeSend(sentryEvent =>
                {
                    if (!consent.Enabled)
                        return null;
                    // Sentry adds a random per-install ID (as the user and as the device ID) even without PII;
                    // reports stay anonymous
                    sentryEvent.User = new SentryUser();
                    sentryEvent.Contexts.Device.DeviceUniqueIdentifier = null;
                    return sentryEvent;
                });
                // Java crashes and ANRs come from the embedded Android SDK, which skips that callback unless this is on
#if ANDROID
                options.Native.EnableBeforeSend = true;
#endif
            });
        }

        /// <summary>The Sentry DSN that CI builds carry (see the csproj); null in local and fork builds.</summary>
        private static string? SentryDsn() =>
            typeof(MauiProgram).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "SentryDsn")?.Value is { Length: > 0 } dsn ? dsn : null;

        /// <summary>
        /// Configures the DecimalEntry control with platform-specific handlers
        /// to show numeric keyboard with decimal support.
        /// </summary>
        private static MauiAppBuilder ConfigureDecimalEntry(this MauiAppBuilder builder)
        {
#if ANDROID
            Platforms.Android.Handlers.DecimalEntryHandler.Configure();
#elif IOS
            Platforms.iOS.Handlers.DecimalEntryHandler.Configure();
#endif
            return builder;
        }

        /// <summary>Android: tapping the tab already shown goes back to its own page.</summary>
        private static MauiAppBuilder ConfigureTabReselect(this MauiAppBuilder builder)
        {
#if ANDROID
            builder.ConfigureMauiHandlers(handlers =>
                handlers.AddHandler(typeof(Shell), typeof(Platforms.Android.Handlers.TabReselectShellRenderer)));
#endif
            return builder;
        }
    }
}
