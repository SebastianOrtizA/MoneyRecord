using Microsoft.Extensions.Logging;
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
            // Initialize localization based on system language
            _ = LocalizationService.Instance;

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("materialdesignicons-webfont.ttf", "MaterialDesignIcons");
                })
                .ConfigureDecimalEntry();

            // Register Infrastructure Services
            builder.Services.AddSingleton<LocalizationService>(_ => LocalizationService.Instance);
            builder.Services.AddSingleton<INavigationService, NavigationService>();
            builder.Services.AddSingleton<IPreferencesService, PreferencesService>();
            builder.Services.AddSingleton<ICategoryIconService, CategoryIconService>();

            // Register Database Infrastructure
            builder.Services.AddSingleton<DatabaseInitializer>();

            // Register Repositories (Interface Segregation)
            builder.Services.AddSingleton<ICategoryRepository, CategoryRepository>();
            builder.Services.AddSingleton<IAccountRepository, AccountRepository>();
            builder.Services.AddSingleton<ITransactionRepository, TransactionRepository>();
            builder.Services.AddSingleton<ITransferRepository, TransferRepository>();

            // Register Business Services
            builder.Services.AddSingleton<IBalanceService, BalanceService>();
            builder.Services.AddSingleton<ITransactionEnrichmentService, TransactionEnrichmentService>();

            // Keep legacy DatabaseService for backward compatibility during migration
            builder.Services.AddSingleton<DatabaseService>();

            // Register ViewModels
            builder.Services.AddSingleton<MainViewModel>();
            builder.Services.AddTransient<AddTransactionViewModel>();
            builder.Services.AddTransient<ManageCategoriesViewModel>();
            builder.Services.AddTransient<ManageAccountsViewModel>();
            builder.Services.AddTransient<TransfersViewModel>();
            builder.Services.AddTransient<AddTransferViewModel>();
            builder.Services.AddTransient<ExpenseReportViewModel>();
            builder.Services.AddTransient<IncomeReportViewModel>();
            builder.Services.AddTransient<FloatingMenuViewModel>();
            builder.Services.AddTransient<BudgetViewModel>();

            // Register Views
            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddTransient<AddTransactionPage>();
            builder.Services.AddTransient<ManageCategoriesPage>();
            builder.Services.AddTransient<ManageAccountsPage>();
            builder.Services.AddTransient<TransfersPage>();
            builder.Services.AddTransient<AddTransferPage>();
            builder.Services.AddTransient<ExpenseReportPage>();
            builder.Services.AddTransient<IncomeReportPage>();
            builder.Services.AddTransient<BudgetPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
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
