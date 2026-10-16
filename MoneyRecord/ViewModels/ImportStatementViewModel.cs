using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using System.ComponentModel;

namespace MoneyRecord.ViewModels
{
    /// <summary>A column of the statement in a picker.</summary>
    public sealed record ColumnOption(int Index, string Name);

    /// <summary>
    /// Bank statement import: pick a CSV file → check the guessed columns → preview (duplicates and unreadable rows
    /// excluded, categories from history) → import into one account, with undo.
    /// </summary>
    public partial class ImportStatementViewModel : ObservableObject
    {
        private readonly IStatementImportService _importService;
        private readonly IAccountRepository _accountRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IEntryDefaultsService _entryDefaults;
        private readonly IDialogService _dialogService;
        private readonly INavigationService _navigationService;
        private readonly IErrorHandler _errorHandler;
        private readonly IExchangeRateService _exchangeRates;
        private readonly IStatementMappingStore _mappingStore;

        private CsvTable? _table;
        private StatementGuess? _guess;
        private StatementMapping? _mapping;
        // Set while a file's guess fills the pickers, so each picker doesn't start its own preview
        private bool _applyingGuess;

        public ImportStatementViewModel(IStatementImportService importService, IAccountRepository accountRepository,
            ICategoryRepository categoryRepository, IEntryDefaultsService entryDefaults, IDialogService dialogService,
            INavigationService navigationService, IErrorHandler errorHandler, IExchangeRateService exchangeRates,
            IStatementMappingStore mappingStore)
        {
            _importService = importService;
            _accountRepository = accountRepository;
            _categoryRepository = categoryRepository;
            _entryDefaults = entryDefaults;
            _dialogService = dialogService;
            _navigationService = navigationService;
            _errorHandler = errorHandler;
            _exchangeRates = exchangeRates;
            _mappingStore = mappingStore;
        }

        [ObservableProperty]
        private bool hasFile;

        [ObservableProperty]
        private string fileSummary = string.Empty;

        [ObservableProperty]
        private List<Account> accounts = [];

        [ObservableProperty]
        private Account? selectedAccount;

        [ObservableProperty]
        private List<ColumnOption> dateColumns = [];

        [ObservableProperty]
        private ColumnOption? selectedDateColumn;

        [ObservableProperty]
        private IReadOnlyList<StatementDate> dateFormats = [];

        [ObservableProperty]
        private StatementDate? selectedDateFormat;

        [ObservableProperty]
        private List<ColumnOption> descriptionColumns = [];

        [ObservableProperty]
        private ColumnOption? selectedDescriptionColumn;

        [ObservableProperty]
        private List<ColumnOption> amountColumns = [];

        [ObservableProperty]
        private ColumnOption? selectedAmountColumn;

        [ObservableProperty]
        private bool positiveIsExpense;

        [ObservableProperty]
        private List<Category> expenseCategories = [];

        [ObservableProperty]
        private Category? selectedExpenseCategory;

        [ObservableProperty]
        private List<Category> incomeCategories = [];

        [ObservableProperty]
        private Category? selectedIncomeCategory;

        [ObservableProperty]
        private IReadOnlyList<ImportRow> rows = [];

        [ObservableProperty]
        private string summary = string.Empty;

        [ObservableProperty]
        private string importButtonText = Plurals.ImportButton(0);

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ImportCommand))]
        private int includedCount;

        [ObservableProperty]
        private bool isBusy;

        /// <summary>The columns and account come from the last import of a file with this header (shown as a note).</summary>
        [ObservableProperty]
        private bool usesSavedMapping;

        /// <summary>Only a file whose dates fit both day-first and month-first needs the format picker.</summary>
        public bool HasDateChoice => DateFormats.Count > 1;

        partial void OnDateFormatsChanged(IReadOnlyList<StatementDate> value) => OnPropertyChanged(nameof(HasDateChoice));

        public async Task InitializeAsync()
        {
            if (Accounts.Count > 0)
                return;

            try
            {
                var accounts = await _accountRepository.GetAllAsync();
                var categories = await _categoryRepository.GetAllAsync();
                ExpenseCategories = _entryDefaults.OrderByRecent(TransactionType.Expense,
                    categories.Where(c => c.Type == CategoryType.Expense));
                IncomeCategories = _entryDefaults.OrderByRecent(TransactionType.Income,
                    categories.Where(c => c.Type == CategoryType.Income));

                _applyingGuess = true;
                Accounts = accounts;
                SelectedAccount = _entryDefaults.DefaultAccount(TransactionType.Expense, accounts);
                SelectedExpenseCategory = _entryDefaults.DefaultCategory(TransactionType.Expense, ExpenseCategories);
                SelectedIncomeCategory = _entryDefaults.DefaultCategory(TransactionType.Income, IncomeCategories);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.ImportFailedToRead);
            }
            finally
            {
                _applyingGuess = false;
            }
        }

        [RelayCommand]
        private async Task ChooseFileAsync()
        {
            try
            {
                var file = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = AppResources.ImportChooseFile,
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.Android, ["text/csv", "text/comma-separated-values", "application/csv",
                            "text/tab-separated-values", "text/plain", "application/vnd.ms-excel"] },
                        { DevicePlatform.WinUI, [".csv", ".txt"] },
                        { DevicePlatform.iOS, ["public.comma-separated-values-text", "public.plain-text"] },
                        { DevicePlatform.MacCatalyst, ["public.comma-separated-values-text", "public.plain-text"] },
                    })
                });
                if (file == null)
                    return;

                IsBusy = true;
                await using var stream = await file.OpenReadAsync();
                // Reading and guessing try every layout on every value: all of it off the UI thread, once
                var (table, guess) = await Task.Run(() =>
                {
                    var table = CsvTable.Read(stream);
                    return (table, StatementMapping.Guess(table));
                });
                if (guess == null)
                {
                    IsBusy = false;
                    await _dialogService.AlertAsync(AppResources.ImportFailedToRead, AppResources.ImportNoTable);
                    return;
                }

                _table = table;
                // A bank's file seen before starts from the choices of its last import
                var saved = _mappingStore.Find(table, guess);
                ApplyGuess(table, guess, saved?.Mapping ?? guess.Mapping, Accounts.FirstOrDefault(a => a.Id == saved?.AccountId));
                UsesSavedMapping = saved != null;
                FileSummary = $"{file.FileName} · {Plurals.Rows(table.Rows.Count)}";
                HasFile = true;
                LoadPreviewCommand.Execute(null);
            }
            catch (Exception ex)
            {
                IsBusy = false;
                await _errorHandler.HandleAsync(ex, AppResources.ImportFailedToRead);
            }
        }

        private void ApplyGuess(CsvTable table, StatementGuess guess, StatementMapping mapping, Account? account)
        {
            var columns = table.Headers.Select((name, i) => new ColumnOption(i, name)).ToList();

            _applyingGuess = true;
            try
            {
                _guess = guess;
                _mapping = mapping;
                DateColumns = columns.Where(c => guess.DateFormats.ContainsKey(c.Index)).ToList();
                AmountColumns = columns.Where(c => guess.AmountColumns.Contains(c.Index)).ToList();
                DescriptionColumns = columns;
                SelectedDateColumn = DateColumns.First(c => c.Index == mapping.DateColumn);
                SelectedAmountColumn = AmountColumns.First(c => c.Index == mapping.AmountColumn);
                SelectedDescriptionColumn = columns[mapping.DescriptionColumn];
                DateFormats = guess.DateFormats[mapping.DateColumn];
                SelectedDateFormat = mapping.DateFormat;
                PositiveIsExpense = mapping.PositiveIsExpense;
                if (account != null)
                    SelectedAccount = account;
            }
            finally
            {
                _applyingGuess = false;
            }
        }

        // Each picker changes the one mapping; the preview follows it

        partial void OnSelectedAccountChanged(Account? value) => Reload();

        partial void OnSelectedDescriptionColumnChanged(ColumnOption? value) =>
            Update(m => value == null ? m : m with { DescriptionColumn = value.Index });

        partial void OnSelectedAmountColumnChanged(ColumnOption? value) =>
            Update(m => value == null ? m : m.WithAmountColumn(_table!, value.Index));

        partial void OnSelectedDateFormatChanged(StatementDate? value) =>
            Update(m => value == null ? m : m with { DateFormat = value });

        partial void OnPositiveIsExpenseChanged(bool value) => Update(m => m with { PositiveIsExpense = value });

        partial void OnSelectedDateColumnChanged(ColumnOption? value)
        {
            if (_applyingGuess || _guess == null || value == null)
                return;

            // Another column may be written in another layout: its own formats (read with the guess), the best one picked
            var formats = _guess.DateFormats[value.Index];
            _applyingGuess = true;
            DateFormats = formats;
            SelectedDateFormat = formats[0];
            _applyingGuess = false;
            Update(m => m with { DateColumn = value.Index, DateFormat = formats[0] });
        }

        private void Update(Func<StatementMapping, StatementMapping> change)
        {
            if (_applyingGuess || _mapping == null)
                return;
            _mapping = change(_mapping);
            UsesSavedMapping = false;
            Reload();
        }

        // A new default category only changes the rows that still have the old one by default (not learned, not chosen)
        partial void OnSelectedExpenseCategoryChanged(Category? oldValue, Category? newValue) =>
            ReplaceDefault(TransactionType.Expense, oldValue, newValue);

        partial void OnSelectedIncomeCategoryChanged(Category? oldValue, Category? newValue) =>
            ReplaceDefault(TransactionType.Income, oldValue, newValue);

        private void ReplaceDefault(TransactionType type, Category? oldValue, Category? newValue)
        {
            foreach (var row in Rows.Where(r => r.Type == type && r.CanImport && !r.IsLearned && r.Category == oldValue))
                row.Category = newValue;
        }

        private void Reload()
        {
            if (!_applyingGuess && HasFile)
                LoadPreviewCommand.Execute(null);
        }

        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadPreviewAsync(CancellationToken cancellationToken)
        {
            if (_table == null || SelectedAccount == null || _mapping is not { } mapping)
                return;

            try
            {
                IsBusy = true;
                var rows = await _importService.PreviewAsync(_table, mapping, SelectedAccount,
                    SelectedExpenseCategory, SelectedIncomeCategory);
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var row in Rows)
                    row.PropertyChanged -= OnRowChanged;
                foreach (var row in rows)
                    row.PropertyChanged += OnRowChanged;
                Rows = rows;
                Summary = string.Format(AppResources.ImportSummary,
                    rows.Count(r => r.CanImport && !r.IsDuplicate), rows.Count(r => r.IsDuplicate), rows.Count(r => !r.CanImport));
                UpdateCount();
                IsBusy = false;
            }
            catch (OperationCanceledException)
            {
                // A newer preview replaces this one and clears the spinner
            }
            catch (Exception ex)
            {
                IsBusy = false;
                await _errorHandler.HandleAsync(ex, AppResources.ImportFailedToRead);
            }
        }

        private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
        {
            // A row without a category (none of its type existed) counts once it gets one
            if (e.PropertyName is nameof(ImportRow.IsIncluded) or nameof(ImportRow.Category))
                UpdateCount();
        }

        private void UpdateCount()
        {
            IncludedCount = Rows.Count(r => r.IsIncluded && r.CanImport && r.Category != null);
            ImportButtonText = Plurals.ImportButton(IncludedCount);
        }

        [RelayCommand]
        private async Task ChangeCategoryAsync(ImportRow? row)
        {
            if (row == null || !row.CanImport)
                return;

            var categories = row.Type == TransactionType.Expense ? ExpenseCategories : IncomeCategories;
            var labels = PickList.UniqueLabels(categories.Select(c => c.Name));
            var choice = await _dialogService.ActionSheetAsync(AppResources.Category, AppResources.Cancel, labels);
            var index = choice == null ? -1 : labels.IndexOf(choice);
            if (index < 0)
                return;

            if (row.Category != categories[index])
            {
                row.Category = categories[index];
                row.IsLearned = false; // the user's choice now, not the history's
            }
        }

        private bool CanImport() => IncludedCount > 0;

        [RelayCommand(CanExecute = nameof(CanImport))]
        private async Task ImportAsync()
        {
            if (SelectedAccount is not { } account)
                return;

            List<int> ids;
            try
            {
                IsBusy = true;
                ids = await _importService.ImportAsync(account, Rows);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.ImportFailed);
                return;
            }
            finally
            {
                IsBusy = false;
            }

            // The next file from this bank starts with these choices
            _mappingStore.Remember(_table!, _mapping!, account.Id);

            // Entries in another currency (perhaps dated before the stored rates) get their rates in the background
            if (!MoneyFormatter.SameCurrency(account.CurrencyCode, MoneyFormatter.BaseCurrency))
                _ = _exchangeRates.UpdateIfEnabledAsync();

            var undo = await _dialogService.ConfirmAsync(AppResources.ImportDoneTitle,
                string.Format(AppResources.ImportDoneMessage, Plurals.Transactions(ids.Count), account.Name),
                AppResources.ImportUndo, AppResources.OK);
            if (undo)
            {
                try
                {
                    await _importService.UndoAsync(ids);
                    await _dialogService.ToastAsync(AppResources.ImportUndone);
                }
                catch (Exception ex)
                {
                    await _errorHandler.HandleAsync(ex, AppResources.ImportFailed);
                }
            }

            await _navigationService.GoBackAsync();
        }
    }
}
