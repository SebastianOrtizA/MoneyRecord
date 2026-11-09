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
    /// <summary>A tag of the list: its amount in the shared period (empty when unused there) and its texts ready for the row.</summary>
    /// <param name="Name">Null for the "no tag" row.</param>
    public sealed record TagRow(string? Name, string Title, string AmountText, string DetailText, string PercentText, double Share,
        bool UsedInPeriod, int TransactionCount);

    /// <summary>
    /// Tags: every tag in use ranked by what it adds up to in the shared period (an entry with several tags counts under each),
    /// then the unused ones. A tap offers its entries, rename, merge and delete.
    /// </summary>
    public partial class TagsViewModel : ObservableObject
    {
        private readonly ITagService _tagService;
        private readonly ITransactionEnrichmentService _enrichmentService;
        private readonly IAccountRepository _accountRepository;
        private readonly IConversionService _conversion;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;
        private readonly INavigationService _navigationService;
        private readonly TransactionsDrillDown _drillDown;

        // While the page is shown: a period or report currency change reloads it
        private IDisposable? _periodWatch;
        private IDisposable? _currencyWatch;
        private List<TagSummary> _all = [];

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
        private IReadOnlyList<TagRow> tags = [];

        public PeriodViewModel Period { get; }

        public TagsViewModel(PeriodViewModel period, ITagService tagService, ITransactionEnrichmentService enrichmentService,
            IAccountRepository accountRepository, IConversionService conversion, IErrorHandler errorHandler,
            IDialogService dialogService, INavigationService navigationService, TransactionsDrillDown drillDown)
        {
            _tagService = tagService;
            _enrichmentService = enrichmentService;
            _accountRepository = accountRepository;
            _conversion = conversion;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
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
                var tagsTask = _tagService.GetAllAsync();
                await Task.WhenAll(transactionsTask, accountsTask, stateTask, tagsTask);
                cancellationToken.ThrowIfCancellationRequested();

                var accounts = await accountsTask;
                var currencies = ReportCurrencies.InUse(accounts, MoneyFormatter.BaseCurrency);
                var state = await stateTask;
                await MainThread.InvokeOnMainThreadAsync(() => Period.SetCurrencies(currencies, state));
                var type = Type;
                var shown = await _conversion.ForReportAsync((await transactionsTask).Where(t => t.Type == type),
                    accounts, Period.IsConverted, Period.ReportCurrency);
                cancellationToken.ThrowIfCancellationRequested();

                _all = await tagsTask;
                var report = TagReportBuilder.Build(shown.Transactions, type);
                HasData = report.HasData;
                TotalText = shown.FormatAmount(report.Total);
                NotConvertedText = shown.NotConvertedText;
                Tags = BuildRows(report, amount => shown.FormatAmount(amount));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A newer load replaced this one
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadTags);
            }
            finally
            {
                // The newer load owns the spinner
                if (!cancellationToken.IsCancellationRequested)
                    IsLoading = false;
            }
        }

        // The tags used in the period by amount, the "no tag" row after them, then the tags not used in it
        private List<TagRow> BuildRows(TagReport report, Func<decimal, string> format)
        {
            var rows = report.Tags
                .Where(t => t.Name != null)
                .Select(t => new TagRow(t.Name, t.Name!, format(t.Amount), Plurals.Transactions(t.Count), t.PercentText, t.Share, true, t.Count))
                .ToList();
            if (report.Tags.FirstOrDefault(t => t.Name == null) is { } untagged)
                rows.Add(new TagRow(null, AppResources.NoTagRow, format(untagged.Amount), Plurals.Transactions(untagged.Count),
                    untagged.PercentText, untagged.Share, true, untagged.Count));

            var used = rows.Where(r => r.Name != null).Select(r => r.Name!).ToHashSet(StringComparer.OrdinalIgnoreCase);
            rows.AddRange(_all.Where(t => !used.Contains(t.Name))
                .Select(t => new TagRow(t.Name, t.Name, string.Empty, AppResources.TagNotUsedInPeriod, string.Empty, 0, false, 0)));
            return rows;
        }

        [RelayCommand]
        private void SetType(bool income)
        {
            if (IsIncome == income)
                return;

            IsIncome = income;
            LoadCommand.Execute(null);
        }

        /// <summary>A row: its entries of this type for the same period, rename, merge or delete (the "no tag" row has none of these).</summary>
        [RelayCommand]
        private async Task OpenTagAsync(TagRow? row)
        {
            if (row?.Name is not { } name || _all.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)) is not { } tag)
                return;

            var actions = new List<(string Label, Func<Task> Run)>();
            if (row.UsedInPeriod)
                actions.Add((AppResources.SeeTagTransactions, () => SeeTransactionsAsync(name)));
            actions.Add((AppResources.RenameTag, () => RenameAsync(tag)));
            // Merging needs another tag to merge into
            if (_all.Count > 1)
                actions.Add((AppResources.MergeTag, () => ChooseMergeTargetAsync(tag)));
            actions.Add((AppResources.DeleteTag, () => DeleteAsync(tag)));

            var labels = actions.Select(a => a.Label).ToList();
            var picked = await _dialogService.ActionSheetAsync(tag.Name, AppResources.Cancel, labels);
            var index = picked == null ? -1 : labels.IndexOf(picked);
            if (index < 0)
                return;

            try
            {
                await actions[index].Run();
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToSaveTag);
            }
        }

        private Task SeeTransactionsAsync(string name)
        {
            _drillDown.Request(new DrillDownTarget(Type, Tag: name));
            return _navigationService.NavigateToKeepingBackAsync($"//{nameof(TransactionsPage)}");
        }

        private async Task RenameAsync(TagSummary tag)
        {
            var typed = await _dialogService.PromptAsync(AppResources.RenameTag,
                string.Format(AppResources.RenameTagMessage, tag.Name), AppResources.Save, AppResources.Cancel, tag.Name);
            var name = TransactionTags.Normalize(typed);
            // Ordinal on purpose: a case-only change ("food" to "Food") is a rename
            if (name.Length == 0 || name == tag.Name)
                return;

            // The name of another tag: the user means the same one, so offer to merge them
            if (_all.FirstOrDefault(t => !string.Equals(t.Name, tag.Name, StringComparison.OrdinalIgnoreCase)
                                         && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)) is { } existing)
            {
                if (await _dialogService.ConfirmAsync(AppResources.TagExistsTitle,
                        string.Format(AppResources.TagExistsMergeMessage, tag.Name, existing.Name),
                        AppResources.MergeButton, AppResources.Cancel))
                    await MergeAsync(tag, existing.Name);
                return;
            }

            await _tagService.RenameAsync(tag.Name, name);
            await LoadCommand.ExecuteAsync(null);
            await _dialogService.ToastAsync(AppResources.TagRenamed);
        }

        private async Task ChooseMergeTargetAsync(TagSummary tag)
        {
            var targets = _all.Where(t => !string.Equals(t.Name, tag.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            var labels = PickList.UniqueLabels(targets.Select(t => t.Name));
            var picked = await _dialogService.ActionSheetAsync(string.Format(AppResources.MergeTagInto, tag.Name),
                AppResources.Cancel, labels);
            var index = picked == null ? -1 : labels.IndexOf(picked);
            if (index >= 0)
                await MergeAsync(tag, targets[index].Name);
        }

        private async Task MergeAsync(TagSummary tag, string target)
        {
            await _tagService.RenameAsync(tag.Name, target);
            await LoadCommand.ExecuteAsync(null);
            await _dialogService.ToastAsync(AppResources.TagsMerged);
        }

        private async Task DeleteAsync(TagSummary tag)
        {
            if (!await _dialogService.ConfirmAsync(AppResources.DeleteTag,
                    string.Format(AppResources.DeleteTagConfirmation, tag.Name, Plurals.Transactions(tag.TransactionCount)),
                    AppResources.DeleteButton, AppResources.Cancel))
                return;

            await _tagService.DeleteAsync(tag.Name);
            await LoadCommand.ExecuteAsync(null);
            await _dialogService.ToastAsync(AppResources.TagDeleted);
        }
    }
}
