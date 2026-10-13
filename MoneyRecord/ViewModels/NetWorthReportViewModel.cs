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
    /// The Net worth report: the total balance of the accounts in the base currency at the end of each month; headline =
    /// today's total against the end of the month before the window. Other currencies are left out (never added
    /// together), and a note says so.
    /// </summary>
    public partial class NetWorthReportViewModel : MonthlyReportViewModel
    {
        private readonly IAccountRepository _accountRepository;
        private readonly IBalanceService _balanceService;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ITransferRepository _transferRepository;
        private readonly ICurrencyService _currencyService;

        [ObservableProperty]
        private ISeries[] netWorthSeries = [];

        /// <summary>"Accounts in COP only" when some accounts are in another currency; empty otherwise.</summary>
        [ObservableProperty]
        private string currencyNote = string.Empty;

        public NetWorthReportViewModel(PeriodViewModel period, IAccountRepository accountRepository, IBalanceService balanceService,
            ITransactionRepository transactionRepository, ITransferRepository transferRepository, ICurrencyService currencyService,
            IErrorHandler errorHandler, INavigationService navigationService, TransactionsDrillDown drillDown)
            : base(period, errorHandler, navigationService, drillDown)
        {
            _accountRepository = accountRepository;
            _balanceService = balanceService;
            _transactionRepository = transactionRepository;
            _transferRepository = transferRepository;
            _currencyService = currencyService;
        }

        protected override async Task BuildAsync(DateTime firstMonth, int months, CancellationToken cancellationToken)
        {
            // One month more at the start: the end of the month before the window is the headline's baseline
            var baselineMonth = firstMonth.AddMonths(-1);
            var accountsTask = _accountRepository.GetAllAsync();
            var balancesTask = _balanceService.GetAllAccountBalancesAsync();
            var transactionsTask = _transactionRepository.GetByDateRangeAsync(baselineMonth, DateTime.MaxValue);
            var transfersTask = _transferRepository.GetByDateRangeAsync(baselineMonth, DateTime.MaxValue);
            await Task.WhenAll(accountsTask, balancesTask, transactionsTask, transfersTask);
            cancellationToken.ThrowIfCancellationRequested();

            var baseCode = _currencyService.GetBaseCurrencyCode();
            var accounts = await accountsTask;
            var counted = accounts
                .Where(a => MoneyFormatter.ResolveCurrencyCode(a.CurrencyCode, baseCode) == baseCode)
                .Select(a => a.Id)
                .ToHashSet();
            var currentTotal = (await balancesTask).Where(b => counted.Contains(b.AccountId)).Sum(b => b.CurrentBalance);

            var points = NetWorthBuilder.Build(currentTotal, counted, await transactionsTask, await transfersTask,
                baselineMonth, months + 1);
            var shown = points.Skip(1).ToList();

            HasData = counted.Count > 0;
            CurrencyNote = counted.Count < accounts.Count ? string.Format(AppResources.NetWorthCurrencyOnly, baseCode) : string.Empty;
            Headline = shown[^1].Value;
            DeltaText = new PeriodDelta(Headline, points[0].Value).Text;

            MonthAxis = ChartFactory.LabelAxis(shown.Select(p => p.Label));
            MoneyAxis = ChartFactory.MoneyAxis(fromZero: false);
            NetWorthSeries = ChartFactory.Lines((month, _) => OpenMonth(month, null),
                new ChartFactory.Line(AppResources.NetWorth, shown.Select(p => p.Value), ChartPalette.Slot(0)));
        }
    }
}
