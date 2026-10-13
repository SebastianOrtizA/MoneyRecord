using Microsoft.Extensions.Logging;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Views;

namespace MoneyRecord
{
    public partial class App : Application
    {
        private readonly IRecurringTransactionService _recurringTransactionService;
        private readonly ISetupService _setupService;
        private readonly Func<SetupPage> _createSetupPage;
        private readonly ILogger<App> _logger;

        public App(IRecurringTransactionService recurringTransactionService, ISetupService setupService,
            Func<SetupPage> createSetupPage, IThemeService themeService, ILogger<App> logger)
        {
            InitializeComponent();
            // Before the first page is created, so it never flashes in the other theme
            themeService.ApplySaved();
            _recurringTransactionService = recurringTransactionService;
            _setupService = setupService;
            _createSetupPage = createSetupPage;
            _logger = logger;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // A new install starts with the setup; it swaps itself for the Shell when finished
            Page page = _setupService.ResolveFirstRunPending()
                ? _createSetupPage().AsFirstRun()
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
