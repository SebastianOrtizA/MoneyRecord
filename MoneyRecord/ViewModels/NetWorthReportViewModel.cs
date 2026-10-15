using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using MoneyRecord.Charts;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>
    /// The Net worth report: the total balance of the accounts in the report's currency at the end of each month; headline =
    /// today's total against the end of the month before the window. Other currencies are left out (never added
    /// together), and a note says so.
    /// </summary>
    public partial class NetWorthReportViewModel : MonthlyReportViewModel
    {
        private readonly IBalanceService _balanceService;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ITransferRepository _transferRepository;

        [ObservableProperty]
        private ISeries[] netWorthSeries = [];

        /// <summary>"Accounts in COP only" when some accounts are in another currency; empty otherwise.</summary>
        [ObservableProperty]
        private string currencyNote = string.Empty;

        public NetWorthReportViewModel(PeriodViewModel period, IAccountRepository accountRepository, IBalanceService balanceService,
            ITransactionRepository transactionRepository, ITransferRepository transferRepository,
            IErrorHandler errorHandler, INavigationService navigationService, TransactionsDrillDown drillDown)
            : base(period, accountRepository, errorHandler, navigationService, drillDown)
        {
            _balanceService = balanceService;
            _transactionRepository = transactionRepository;
            _transferRepository = transferRepository;
        }

        protected override async Task BuildAsync(DateTime firstMonth, int months, CancellationToken cancellationToken)
        {
            // One month more at the start: the end of the month before the window is the headline's baseline
            var baselineMonth = firstMonth.AddMonths(-1);
            var accountsTask = LoadAccountsAsync();
            var balancesTask = _balanceService.GetAllAccountBalancesAsync();
            var transactionsTask = _transactionRepository.GetByDateRangeAsync(baselineMonth, DateTime.MaxValue);
            var transfersTask = _transferRepository.GetByDateRangeAsync(baselineMonth, DateTime.MaxValue);
            await Task.WhenAll(accountsTask, balancesTask, transactionsTask, transfersTask);
            cancellationToken.ThrowIfCancellationRequested();

            var code = Period.ReportCurrency;
            var accounts = await accountsTask;
            var counted = accounts
                .Where(a => MoneyFormatter.ResolveCurrencyCode(a.CurrencyCode, MoneyFormatter.BaseCurrency) == code)
                .Select(a => a.Id)
                .ToHashSet();
            var currentTotal = (await balancesTask).Where(b => counted.Contains(b.AccountId)).Sum(b => b.CurrentBalance);

            var points = NetWorthBuilder.Build(currentTotal, counted, await transactionsTask, await transfersTask,
                baselineMonth, months + 1);
            var shown = points.Skip(1).ToList();

            HasData = counted.Count > 0;
            CurrencyNote = counted.Count < accounts.Count ? string.Format(AppResources.NetWorthCurrencyOnly, code) : string.Empty;
            HeadlineText = MoneyFormatter.Format(shown[^1].Value, code);
            DeltaText = new PeriodDelta(shown[^1].Value, points[0].Value).Text;

            MonthAxis = ChartFactory.LabelAxis(shown.Select(p => p.Label));
            MoneyAxis = ChartFactory.MoneyAxis(code, fromZero: false);
            NetWorthSeries = ChartFactory.Lines((month, _) => OpenMonth(month, null), code,
                new ChartFactory.Line(AppResources.NetWorth, shown.Select(p => p.Value), ChartPalette.Slot(0)));
        }
    }
}
