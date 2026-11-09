using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>A category of the comparison with its texts ready for the row.</summary>
    public sealed record CompareRowItem(string Name, string AmountsText, string DeltaText, DeltaTone DeltaTone);

    /// <summary>
    /// Two periods side by side (A against B), category by category. The periods are free: a preset or the four dates.
    /// The shared <see cref="PeriodViewModel"/> only supplies the report currency (chips, "Show in").
    /// </summary>
    public partial class ComparePeriodsViewModel : ObservableObject
    {
        private readonly ITransactionEnrichmentService _enrichmentService;
        private readonly IAccountRepository _accountRepository;
        private readonly IConversionService _conversion;
        private readonly IErrorHandler _errorHandler;
        private readonly TimeProvider _clock;

        // While the page is shown: a report currency change reloads it
        private IDisposable? _currencyWatch;

        // True while a preset sets the four dates, so they cause one load, not four
        private bool _applyingPreset;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool isIncome;

        [ObservableProperty]
        private bool hasData;

        [ObservableProperty]
        private DateTime startA;

        [ObservableProperty]
        private DateTime endA;

        [ObservableProperty]
        private DateTime startB;

        [ObservableProperty]
        private DateTime endB;

        /// <summary>The headline: A's total, B's total.</summary>
        [ObservableProperty]
        private string totalsText = string.Empty;

        /// <summary>"▲ 12% vs the other period"; empty when both are zero.</summary>
        [ObservableProperty]
        private string deltaText = string.Empty;

        [ObservableProperty]
        private DeltaTone deltaTone;

        /// <summary>"No exchange rate yet: ¥1,000" while converted, for what had no rate; empty otherwise.</summary>
        [ObservableProperty]
        private string notConvertedText = string.Empty;

        [ObservableProperty]
        private IReadOnlyList<CompareRowItem> rows = [];

        public PeriodViewModel Period { get; }

        public ComparePeriodsViewModel(PeriodViewModel period, ITransactionEnrichmentService enrichmentService,
            IAccountRepository accountRepository, IConversionService conversion, IErrorHandler errorHandler, TimeProvider clock)
        {
            _enrichmentService = enrichmentService;
            _accountRepository = accountRepository;
            _conversion = conversion;
            _errorHandler = errorHandler;
            _clock = clock;
            Period = period;
            ApplyPreset(ComparePreset.ThisMonthVsLast, load: false);
        }

        private TransactionType Type => IsIncome ? TransactionType.Income : TransactionType.Expense;

        /// <summary>The page appeared: load, and follow the report currency until <see cref="Deactivate"/>.</summary>
        public Task InitializeAsync()
        {
            _currencyWatch ??= Period.WatchCurrency(() => LoadCommand.Execute(null));
            return LoadCommand.ExecuteAsync(null);
        }

        public void Deactivate()
        {
            _currencyWatch?.Dispose();
            _currencyWatch = null;
        }

        partial void OnStartAChanged(DateTime value) => DateChanged();

        partial void OnEndAChanged(DateTime value) => DateChanged();

        partial void OnStartBChanged(DateTime value) => DateChanged();

        partial void OnEndBChanged(DateTime value) => DateChanged();

        private void DateChanged()
        {
            if (!_applyingPreset)
                LoadCommand.Execute(null);
        }

        [RelayCommand]
        private void SetType(bool income)
        {
            if (IsIncome == income)
                return;

            IsIncome = income;
            LoadCommand.Execute(null);
        }

        [RelayCommand]
        private void Preset(ComparePreset preset) => ApplyPreset(preset, load: true);

        private void ApplyPreset(ComparePreset preset, bool load)
        {
            var (a, b) = ComparePresets.Resolve(preset, _clock.GetLocalNow().DateTime);
            _applyingPreset = true;
            try
            {
                // The pickers choose days: the end of a range is its last day
                StartA = a.Start.Date;
                EndA = a.End.Date;
                StartB = b.Start.Date;
                EndB = b.End.Date;
            }
            finally
            {
                _applyingPreset = false;
            }

            if (load)
                LoadCommand.Execute(null);
        }

        /// <summary>Each run cancels the one in progress, so an older choice's results never overwrite a newer one's.</summary>
        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadAsync(CancellationToken cancellationToken)
        {
            try
            {
                IsLoading = true;
                var (startA, endA) = (StartA.Date, EndA.Date.AddDays(1).AddTicks(-1));
                var (startB, endB) = (StartB.Date, EndB.Date.AddDays(1).AddTicks(-1));
                var transactionsATask = _enrichmentService.GetEnrichedTransactionsAsync(startA, endA);
                var transactionsBTask = _enrichmentService.GetEnrichedTransactionsAsync(startB, endB);
                var accountsTask = _accountRepository.GetAllAsync();
                var stateTask = _conversion.GetStateAsync();
                await Task.WhenAll(transactionsATask, transactionsBTask, accountsTask, stateTask);
                cancellationToken.ThrowIfCancellationRequested();

                var accounts = await accountsTask;
                var currencies = ReportCurrencies.InUse(accounts, MoneyFormatter.BaseCurrency);
                var state = await stateTask;
                await MainThread.InvokeOnMainThreadAsync(() => Period.SetCurrencies(currencies, state));
                var type = Type;
                var shownA = await _conversion.ForReportAsync((await transactionsATask).Where(t => t.Type == type),
                    accounts, Period.IsConverted, Period.ReportCurrency);
                var shownB = await _conversion.ForReportAsync((await transactionsBTask).Where(t => t.Type == type),
                    accounts, Period.IsConverted, Period.ReportCurrency);
                cancellationToken.ThrowIfCancellationRequested();

                var report = PeriodCompareBuilder.Build(shownA.Transactions, shownB.Transactions, type, AppResources.Unknown);
                var delta = report.Delta;
                HasData = report.HasData;
                TotalsText = string.Format(AppResources.CompareRowAmounts, shownA.FormatAmount(report.TotalA), shownB.FormatAmount(report.TotalB));
                DeltaText = delta.TextVs(AppResources.CompareDeltaVs);
                DeltaTone = delta.ToneFor(higherIsBetter: IsIncome);
                NotConvertedText = string.Join(" · ", new[] { shownA.NotConvertedText, shownB.NotConvertedText }.Where(t => !string.IsNullOrEmpty(t)).Distinct());
                Rows = [.. report.Rows.Select(r => new CompareRowItem(r.Name,
                    string.Format(AppResources.CompareRowAmounts, shownA.FormatAmount(r.AmountA), shownB.FormatAmount(r.AmountB)),
                    r.Delta.Change is null ? "–" : r.Delta.TextVs("{0}"), r.Delta.ToneFor(higherIsBetter: IsIncome)))];
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
    }
}
