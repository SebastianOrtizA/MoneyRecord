using Microsoft.Extensions.Logging;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());

            window.Created += (s, e) =>
            {
                _ = ProcessRecurringTransactionsAsync();
            };

            return window;
        }

        private async Task ProcessRecurringTransactionsAsync()
        {
            try
            {
                var service = IPlatformApplication.Current?.Services.GetService<IRecurringTransactionService>();
                if (service != null)
                {
                    await service.ProcessDueTransactionsAsync();
                }
            }
            catch (Exception ex)
            {
                IPlatformApplication.Current?.Services.GetService<ILogger<App>>()?.LogError(ex, "Failed to process recurring transactions");
            }
        }
    }
}