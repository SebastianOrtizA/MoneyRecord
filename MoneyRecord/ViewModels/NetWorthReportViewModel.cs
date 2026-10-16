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
        private readonly IExchangeRateService _exchangeRates;

        [ObservableProperty]
        private ISeries[] netWorthSeries = [];

        /// <summary>"Accounts in COP only" when some accounts are in another currency; empty otherwise.</summary>
        [ObservableProperty]
        private string currencyNote = string.Empty;

        public NetWorthReportViewModel(PeriodViewModel period, IAccountRepository accountRepository, IBalanceService balanceService,
            ITransactionRepository transactionRepository, ITransferRepository transferRepository, IConversionService conversion,
            IExchangeRateService exchangeRates, IErrorHandler errorHandler, INavigationService navigationService,
            TransactionsDrillDown drillDown)
            : base(period, accountRepository, conversion, errorHandler, navigationService, drillDown)
        {
            _exchangeRates = exchangeRates;
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
            var balances = await balancesTask;
            var currencyOf = accounts.ToDictionary(a => a.Id, a => MoneyFormatter.ResolveCurrencyCode(a.CurrencyCode, MoneyFormatter.BaseCurrency));
            var byCurrency = currencyOf.GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g =>
            {
                var ids = g.Select(kv => kv.Key).ToHashSet();
                return (CurrentTotal: balances.Where(b => ids.Contains(b.AccountId)).Sum(b => b.CurrentBalance),
                    AccountIds: (IReadOnlyCollection<int>)ids);
            });

            List<NetWorthPoint> points;
            if (Period.IsConverted)
            {
                // Each currency valued at each month end's rate, then added up
                (points, var notConverted) = NetWorthBuilder.BuildConverted(byCurrency, await transactionsTask, await transfersTask,
                    baselineMonth, months + 1, await _exchangeRates.GetConverterAsync(), code, DateTime.Today);
                HasData = byCurrency.Count > notConverted.Count;
                CurrencyNote = notConverted.Count > 0
                    ? string.Format(AppResources.NotConvertedNote, string.Join(", ", notConverted))
                    : string.Empty;
            }
            else
            {
                var counted = byCurrency.GetValueOrDefault(code);
                points = NetWorthBuilder.Build(counted.CurrentTotal, counted.AccountIds ?? [], await transactionsTask,
                    await transfersTask, baselineMonth, months + 1);
                HasData = counted.AccountIds?.Count > 0;
                CurrencyNote = byCurrency.Count > 1 ? string.Format(AppResources.NetWorthCurrencyOnly, code) : string.Empty;
            }

            var shown = points.Skip(1).ToList();
            var approximate = Period.IsConverted ? MoneyFormatter.ApproximateMark : string.Empty;
            HeadlineText = approximate + MoneyFormatter.Format(shown[^1].Value, code);
            DeltaText = new PeriodDelta(shown[^1].Value, points[0].Value).Text;

            MonthAxis = ChartFactory.LabelAxis(shown.Select(p => p.Label));
            MoneyAxis = ChartFactory.MoneyAxis(code, fromZero: false);
            NetWorthSeries = ChartFactory.Lines((month, _) => OpenMonth(month, null), code,
                new ChartFactory.Line(AppResources.NetWorth, shown.Select(p => p.Value), ChartPalette.Slot(0)));
        }
    }
}
