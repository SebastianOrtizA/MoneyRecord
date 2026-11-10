using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Pdf;

namespace MoneyRecord.ViewModels
{
    /// <summary>
    /// A calendar year in numbers (see <see cref="YearReviewBuilder"/>): section cards, and sharing as a PDF or plain text,
    /// with or without amounts. Amounts stay out of the cards while Hide balances is on. The shared
    /// <see cref="PeriodViewModel"/> only supplies the report currency (chips, "Show in").
    /// </summary>
    public partial class YearReviewViewModel : ObservableObject
    {
        private readonly ITransactionEnrichmentService _enrichmentService;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IConversionService _conversion;
        private readonly IPrivacySettings _privacy;
        private readonly IFileSharer _sharer;
        private readonly IDialogService _dialogs;
        private readonly IErrorHandler _errorHandler;
        private readonly TimeProvider _clock;

        // While the page is shown: a report currency change reloads it
        private IDisposable? _currencyWatch;

        // The loaded year, what the share writes
        private YearReview? _review;

        private int _firstYear;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PreviousYearCommand), nameof(NextYearCommand))]
        private int year;

        [ObservableProperty]
        private bool hasData;

        /// <summary>"No exchange rate yet: ¥1,000" while converted, for what had no rate; empty otherwise.</summary>
        [ObservableProperty]
        private string notConvertedText = string.Empty;

        [ObservableProperty]
        private IReadOnlyList<YearReviewSection> sections = [];

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ShareCommand))]
        private bool isSharing;

        public PeriodViewModel Period { get; }

        public YearReviewViewModel(PeriodViewModel period, ITransactionEnrichmentService enrichmentService,
            ITransactionRepository transactionRepository, IAccountRepository accountRepository,
            ICategoryRepository categoryRepository, IConversionService conversion, IPrivacySettings privacy,
            IFileSharer sharer, IDialogService dialogs, IErrorHandler errorHandler, TimeProvider clock)
        {
            _enrichmentService = enrichmentService;
            _transactionRepository = transactionRepository;
            _accountRepository = accountRepository;
            _categoryRepository = categoryRepository;
            _conversion = conversion;
            _privacy = privacy;
            _sharer = sharer;
            _dialogs = dialogs;
            _errorHandler = errorHandler;
            _clock = clock;
            Period = period;
            Year = Today.Year;
            _firstYear = Year;
        }

        private DateTime Today => _clock.GetLocalNow().DateTime;

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

        partial void OnYearChanged(int value) => LoadCommand.Execute(null);

        private bool CanGoPrevious() => Year > _firstYear;

        private bool CanGoNext() => Year < Today.Year;

        [RelayCommand(CanExecute = nameof(CanGoPrevious))]
        private void PreviousYear() => Year--;

        [RelayCommand(CanExecute = nameof(CanGoNext))]
        private void NextYear() => Year++;

        /// <summary>Each run cancels the one in progress, so an older year's results never overwrite a newer one's.</summary>
        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadAsync(CancellationToken cancellationToken)
        {
            try
            {
                IsLoading = true;
                var year = Year;
                var transactionsTask = _enrichmentService.GetEnrichedTransactionsAsync(new DateTime(year, 1, 1),
                    new DateTime(year, 12, 31).AddDays(1).AddTicks(-1));
                var accountsTask = _accountRepository.GetAllAsync();
                var categoriesTask = _categoryRepository.GetByTypeAsync(CategoryType.Expense);
                var stateTask = _conversion.GetStateAsync();
                var firstDatesTask = _transactionRepository.GetFirstDatesByCurrencyAsync(MoneyFormatter.BaseCurrency);
                await Task.WhenAll(transactionsTask, accountsTask, categoriesTask, stateTask, firstDatesTask);
                cancellationToken.ThrowIfCancellationRequested();

                var accounts = await accountsTask;
                var currencies = ReportCurrencies.InUse(accounts, MoneyFormatter.BaseCurrency);
                var state = await stateTask;
                await MainThread.InvokeOnMainThreadAsync(() => Period.SetCurrencies(currencies, state));
                var shown = await _conversion.ForReportAsync(await transactionsTask, accounts, Period.IsConverted,
                    Period.ReportCurrency);
                cancellationToken.ThrowIfCancellationRequested();

                var firstDates = await firstDatesTask;
                var firstYear = firstDates.Count == 0 ? Today.Year : firstDates.Values.Min().Year;
                var categoryIds = (await categoriesTask).Select(c => c.Id);
                var review = YearReviewBuilder.Build(year, shown.Transactions, categoryIds, shown.CurrencyCode,
                    shown.IsApproximate, AppResources.Unknown, Today);

                _review = review;
                _firstYear = Math.Min(firstYear, Today.Year);
                PreviousYearCommand.NotifyCanExecuteChanged();
                HasData = review.HasData;
                NotConvertedText = shown.NotConvertedText;
                Sections = YearReviewSections.Build(review, includeAmounts: !_privacy.HideBalances);
                ShareCommand.NotifyCanExecuteChanged();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A newer run took over
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadReportData);
            }
            finally
            {
                // Only the last run clears the spinner
                if (!cancellationToken.IsCancellationRequested)
                    IsLoading = false;
            }
        }

        private bool CanShare() => !IsSharing && HasData;

        /// <summary>The share button: PDF or text, with amounts unless Hide balances is on (then only without), then the share sheet.</summary>
        [RelayCommand(CanExecute = nameof(CanShare))]
        private async Task ShareAsync()
        {
            if (_review is not { HasData: true } review)
                return;

            IsSharing = true;
            try
            {
                var pdf = AppResources.YearShareAsPdf;
                var text = AppResources.YearShareAsText;
                var options = new List<string>();
                if (!_privacy.HideBalances)
                    options.AddRange([pdf, text]);
                options.AddRange([string.Format(AppResources.YearShareNoAmounts, pdf),
                    string.Format(AppResources.YearShareNoAmounts, text)]);

                var choice = await _dialogs.ActionSheetAsync(AppResources.YearShareTitle, AppResources.Cancel, options);
                var index = choice == null ? -1 : options.IndexOf(choice);
                if (index < 0)
                    return;

                // The last two options are the ones without amounts
                var withoutAmounts = index >= options.Count - 2;
                var isPdf = options[index] == pdf || options[index] == string.Format(AppResources.YearShareNoAmounts, pdf);

                if (isPdf)
                {
                    var path = _sharer.NewCacheFile("MoneyRecord_Year", ".pdf");
                    await Task.Run(() =>
                    {
                        using var file = File.Create(path);
                        YearReviewPdf.Write(review, !withoutAmounts, file);
                    });
                    await _sharer.ShareAsync(path, AppResources.YearShareTitle);
                }
                else
                {
                    await _sharer.ShareTextAsync(YearReviewSections.ToText(review, !withoutAmounts), AppResources.YearShareTitle);
                }
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToExportReport);
            }
            finally
            {
                IsSharing = false;
            }
        }
    }
}
