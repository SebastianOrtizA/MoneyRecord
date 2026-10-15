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
            CurrencyService.LoadBaseCurrency(preferences);
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

            // Register Repositories (Interface Segregation)
            builder.Services.AddSingleton<ICategoryRepository, CategoryRepository>();
            builder.Services.AddSingleton<IAccountRepository, AccountRepository>();
            builder.Services.AddSingleton<ITransactionRepository, TransactionRepository>();
            builder.Services.AddSingleton<ITransferRepository, TransferRepository>();
            builder.Services.AddSingleton<IBudgetRepository, BudgetRepository>();
            builder.Services.AddSingleton<IRecurringTransactionRepository, RecurringTransactionRepository>();

            // Register Business Services
            builder.Services.AddSingleton<IBalanceService, BalanceService>();
            builder.Services.AddSingleton<ITransactionEnrichmentService, TransactionEnrichmentService>();
            builder.Services.AddSingleton<ILedgerService, LedgerService>();
            builder.Services.AddSingleton<IBudgetSummaryService, BudgetSummaryService>();
            builder.Services.AddSingleton<IRecurringTransactionService, RecurringTransactionService>();
            builder.Services.AddSingleton<IErrorHandler, ErrorHandler>();
            builder.Services.AddSingleton<IDataExportService, DataExportService>();
            builder.Services.AddSingleton<IAccountService, AccountService>();
            builder.Services.AddSingleton<ICategoryService, CategoryService>();
            builder.Services.AddSingleton<IEntryDefaultsService, EntryDefaultsService>();
            builder.Services.AddSingleton<ISetupService, SetupService>();
            // A report asks the Transactions tab to open filtered
            builder.Services.AddSingleton<TransactionsDrillDown>();

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
            builder.Services.AddTransient<AddEntryViewModel>();
            builder.Services.AddTransient<BudgetViewModel>();
            builder.Services.AddTransient<RecurringTransactionsViewModel>();
            builder.Services.AddTransient<SettingsViewModel>();
            builder.Services.AddTransient<SetupViewModel>();
            builder.Services.AddTransient<AboutViewModel>();
            builder.Services.AddTransient<LicenseViewModel>();
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
            builder.Services.AddTransient<RecurringTransactionsPage>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<SetupPage>();
            builder.Services.AddTransient<AboutPage>();
            builder.Services.AddTransient<LicensePage>();
            // App creates it only on a new install
            builder.Services.AddTransient<Func<SetupPage>>(services => () => services.GetRequiredService<SetupPage>());
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
                    // Sentry adds a random per-install ID as the user even without PII; reports stay anonymous
                    sentryEvent.User = new SentryUser();
                    return sentryEvent;
                });
                // Java crashes and ANRs come from the embedded Android SDK, which skips that callback unless this is on
                options.Native.EnableBeforeSend = true;
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
    }
}
