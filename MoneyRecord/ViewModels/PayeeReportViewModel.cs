using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Views;

namespace MoneyRecord.ViewModels
{
    /// <summary>A payee of the report's ranked list, with its texts ready for the row.</summary>
    public sealed record PayeeRow(int? PayeeId, string Name, string AmountText, string DetailText, string PercentText, double Share);

    /// <summary>
    /// Who the money goes to (or comes from) in the shared period: payees ranked by amount with share, count and average
    /// ticket, entries without a payee last. A row opens that payee's entries on the Transactions tab.
    /// </summary>
    public partial class PayeeReportViewModel : ObservableObject
    {
        private readonly ITransactionEnrichmentService _enrichmentService;
        private readonly IAccountRepository _accountRepository;
        private readonly IConversionService _conversion;
        private readonly IErrorHandler _errorHandler;
        private readonly INavigationService _navigationService;
        private readonly TransactionsDrillDown _drillDown;

        // While the page is shown: a period or report currency change reloads it
        private IDisposable? _periodWatch;
        private IDisposable? _currencyWatch;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool isIncome;

        [ObservableProperty]
        private bool hasData;

        [ObservableProperty]
        private string totalText = string.Empty;

        /// <summary>"No exchange rate yet: ¥1,000" while converted, for what had no rate; empty otherwise.</summary>
        [ObservableProperty]
        private string notConvertedText = string.Empty;

        [ObservableProperty]
        private IReadOnlyList<PayeeRow> payees = [];

        public PeriodViewModel Period { get; }

        public PayeeReportViewModel(PeriodViewModel period, ITransactionEnrichmentService enrichmentService,
            IAccountRepository accountRepository, IConversionService conversion, IErrorHandler errorHandler,
            INavigationService navigationService, TransactionsDrillDown drillDown)
        {
            _enrichmentService = enrichmentService;
            _accountRepository = accountRepository;
            _conversion = conversion;
            _errorHandler = errorHandler;
            _navigationService = navigationService;
            _drillDown = drillDown;
            Period = period;
        }

        private TransactionType Type => IsIncome ? TransactionType.Income : TransactionType.Expense;

        /// <summary>The page appeared: load, and follow the shared period until <see cref="Deactivate"/>.</summary>
        public Task InitializeAsync()
        {
            _periodWatch ??= Period.WatchChanges(() => LoadCommand.Execute(null));
            _currencyWatch ??= Period.WatchCurrency(() => LoadCommand.Execute(null));
            return LoadCommand.ExecuteAsync(null);
        }

        public void Deactivate()
        {
            _periodWatch?.Dispose();
            _periodWatch = null;
            _currencyWatch?.Dispose();
            _currencyWatch = null;
        }

        /// <summary>Each run cancels the one in progress, so an older period's results never overwrite a newer one's.</summary>
        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadAsync(CancellationToken cancellationToken)
        {
            try
            {
                IsLoading = true;
                var (start, end) = Period.Range;
                var transactionsTask = _enrichmentService.GetEnrichedTransactionsAsync(start, end);
                var accountsTask = _accountRepository.GetAllAsync();
                var stateTask = _conversion.GetStateAsync();
                await Task.WhenAll(transactionsTask, accountsTask, stateTask);
                cancellationToken.ThrowIfCancellationRequested();

                var accounts = await accountsTask;
                var currencies = ReportCurrencies.InUse(accounts, MoneyFormatter.BaseCurrency);
                var state = await stateTask;
                await MainThread.InvokeOnMainThreadAsync(() => Period.SetCurrencies(currencies, state));
                var type = Type;
                var shown = await _conversion.ForReportAsync((await transactionsTask).Where(t => t.Type == type),
                    accounts, Period.IsConverted, Period.ReportCurrency);
                cancellationToken.ThrowIfCancellationRequested();

                var report = PayeeReportBuilder.Build(shown.Transactions, type, AppResources.NoPayeeRow);
                HasData = report.HasData;
                TotalText = shown.FormatAmount(report.Total);
                NotConvertedText = shown.NotConvertedText;
                Payees = [.. report.Payees.Select(p => new PayeeRow(p.PayeeId, p.Name, shown.FormatAmount(p.Amount),
                    string.Format(AppResources.PayeeRowDetail, p.Count, shown.FormatAmount(p.AverageTicket)), p.PercentText, p.Share))];
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A newer load replaced this one
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadReportData);
            }
            finally
            {
                // The newer load owns the spinner
                if (!cancellationToken.IsCancellationRequested)
                    IsLoading = false;
            }
        }

        [RelayCommand]
        private void SetType(bool income)
        {
            if (IsIncome == income)
                return;

            IsIncome = income;
            LoadCommand.Execute(null);
        }

        /// <summary>A row: that payee's entries of this type for the same period (the "no payee" row has none to open).</summary>
        [RelayCommand]
        private Task OpenPayeeAsync(PayeeRow? row)
        {
            if (row?.PayeeId is not int id)
                return Task.CompletedTask;

            _drillDown.Request(new DrillDownTarget(Type, null, new FilterOption(id, row.Name)));
            return _navigationService.NavigateToKeepingBackAsync($"//{nameof(TransactionsPage)}");
        }
    }
}
