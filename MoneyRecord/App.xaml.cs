using Microsoft.Extensions.Logging;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Views;

namespace MoneyRecord
{
    public partial class App : Application
    {
        private readonly IRecurringTransactionService _recurringTransactionService;
        private readonly ISetupService _setupService;
        private readonly IServiceProvider _services;
        private readonly ILogger<App> _logger;

        public App(IRecurringTransactionService recurringTransactionService, ISetupService setupService,
            IServiceProvider services, ILogger<App> logger)
        {
            InitializeComponent();
            _recurringTransactionService = recurringTransactionService;
            _setupService = setupService;
            _services = services;
            _logger = logger;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // A new install starts with the setup; it swaps itself for the Shell when finished
            Page page = _setupService.IsFirstRunPending()
                ? _services.GetRequiredService<SetupPage>().AsFirstRun()
                : new AppShell();
            var window = new Window(page);

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
