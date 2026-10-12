using Microsoft.Extensions.Logging;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord
{
    public partial class App : Application
    {
        private readonly IRecurringTransactionService _recurringTransactionService;
        private readonly ILogger<App> _logger;

        public App(IRecurringTransactionService recurringTransactionService, ILogger<App> logger)
        {
            InitializeComponent();
            _recurringTransactionService = recurringTransactionService;
            _logger = logger;
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
                await _recurringTransactionService.ProcessDueTransactionsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process recurring transactions");
            }
        }
    }
}
