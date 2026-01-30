using Microsoft.Extensions.Logging;
using MoneyRecord.Services;
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
                });

            // Register Services
            builder.Services.AddSingleton<LocalizationService>(_ => LocalizationService.Instance);
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

            // Register Views
            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddTransient<AddTransactionPage>();
            builder.Services.AddTransient<ManageCategoriesPage>();
            builder.Services.AddTransient<ManageAccountsPage>();
            builder.Services.AddTransient<TransfersPage>();
            builder.Services.AddTransient<AddTransferPage>();
            builder.Services.AddTransient<ExpenseReportPage>();
            builder.Services.AddTransient<IncomeReportPage>();

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
