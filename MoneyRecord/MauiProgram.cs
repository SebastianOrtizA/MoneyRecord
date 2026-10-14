using CommunityToolkit.Maui;
using LiveChartsCore.SkiaSharpView.Maui;
using Microsoft.Extensions.Logging;
using MoneyRecord.Helpers;
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
                .ConfigureLogging(paths.LogsFolder);

            // Register Infrastructure Services
            builder.Services.AddSingleton<LocalizationService>(_ => LocalizationService.Instance);
            builder.Services.AddSingleton<INavigationService, NavigationService>();
            builder.Services.AddSingleton<IDialogService, DialogService>();
            builder.Services.AddSingleton<IPreferencesService, PreferencesService>();
            builder.Services.AddSingleton<ICategoryIconService, CategoryIconService>();
            builder.Services.AddSingleton<ICurrencyService, CurrencyService>();
            builder.Services.AddSingleton<IThemeService, ThemeService>();

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
