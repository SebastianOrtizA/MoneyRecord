using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<ImportStatementViewModel> _logger;

        private CsvTable? _table;
        private StatementGuess? _guess;
        private StatementMapping? _mapping;
        // An OFX or QIF file's movements (no column mapping); a QIF is kept to be read again when its date order changes
        private ParsedStatement? _statement;
        private QifParser? _qif;
        private StatementPreset? _preset;
        // Set while a file's guess fills the pickers, so each picker doesn't start its own preview
        private bool _applyingGuess;

        public ImportStatementViewModel(IStatementImportService importService, IAccountRepository accountRepository,
            ICategoryRepository categoryRepository, IEntryDefaultsService entryDefaults, IDialogService dialogService,
            INavigationService navigationService, IErrorHandler errorHandler, IExchangeRateService exchangeRates,
            IStatementMappingStore mappingStore, ILogger<ImportStatementViewModel> logger)
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
            _logger = logger;
        }

        [ObservableProperty]
        private bool hasFile;

        /// <summary>A CSV file's columns are chosen on the screen; an OFX or QIF file has none.</summary>
        [ObservableProperty]
        private bool showsMapping = true;

        /// <summary>A QIF file whose dates fit day-first and month-first: the one thing to choose.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowsQifDate))]
        private bool isQif;

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

        /// <summary>The file is another app's export (YNAB, Mint): its columns are read by the preset, with no pickers.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasPreset))]
        [NotifyPropertyChangedFor(nameof(PresetNote))]
        [NotifyPropertyChangedFor(nameof(ShowsQifDate))]
        private string presetName = string.Empty;

        /// <summary>The file names a category per row: offer to create the ones the app lacks.</summary>
        [ObservableProperty]
        private bool hasCategoryColumn;

        [ObservableProperty]
        private bool createMissingCategories;

        public bool HasPreset => PresetName.Length > 0;

        public string PresetNote => string.Format(AppResources.ImportPresetNote, PresetName);

        partial void OnCreateMissingCategoriesChanged(bool value) => Reload();

        /// <summary>Only a file whose dates fit both day-first and month-first needs the format picker.</summary>
        public bool HasDateChoice => DateFormats.Count > 1;

        public bool ShowsQifDate => (IsQif || HasPreset) && HasDateChoice;

        partial void OnDateFormatsChanged(IReadOnlyList<StatementDate> value)
        {
            OnPropertyChanged(nameof(HasDateChoice));
            OnPropertyChanged(nameof(ShowsQifDate));
        }

        public async Task InitializeAsync()
        {
            if (Accounts.Count > 0)
                return;

            try
            {
                var accounts = await _accountRepository.GetOwnAsync();
                var categories = await _categoryRepository.GetAllAsync();
                ExpenseCategories = _entryDefaults.OrderByRecent(TransactionType.Expense,
                    categories.Where(c => c.Type == CategoryType.Expense && !c.IsTravel));
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
                        // Android has no type for .ofx or .qif (they arrive as octet-stream): any file, told apart by its name
                        { DevicePlatform.Android, ["*/*"] },
                        { DevicePlatform.WinUI, [".csv", ".txt", ".ofx", ".qfx", ".qif"] },
                        { DevicePlatform.iOS, ["public.data"] },
                        { DevicePlatform.MacCatalyst, ["public.data"] },
                    })
                });
                if (file == null)
                    return;

                IsBusy = true;
                await using var stream = await file.OpenReadAsync();
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (extension is ".ofx" or ".qfx" or ".qif")
                {
                    await OpenStatementFileAsync(file.FileName, stream, isQif: extension == ".qif");
                    return;
                }

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
                _statement = null;
                _qif = null;
                IsQif = false;
                var preset = StatementPresets.Detect(table, guess);
                _preset = preset;
                PresetName = preset?.Name ?? string.Empty;
                HasCategoryColumn = preset?.Mapping.CategoryColumn != null;
                if (preset != null)
                {
                    // Another app's export reads by its own columns: nothing to pick, nothing to remember
                    _guess = guess;
                    _mapping = preset.Mapping;
                    ShowsMapping = false;
                    UsesSavedMapping = false;
                    // Dates that fit day-first and month-first stay choosable: the one thing a preset can't know
                    _applyingGuess = true;
                    try
                    {
                        DateFormats = guess.DateFormats[preset.Mapping.DateColumn];
                        SelectedDateFormat = preset.Mapping.DateFormat;
                    }
                    finally
                    {
                        _applyingGuess = false;
                    }
                }
                else
                {
                    ShowsMapping = true;
                    // A bank's file seen before starts from the choices of its last import
                    var saved = _mappingStore.Find(table, guess);
                    var savedAccount = Accounts.FirstOrDefault(a => a.Id == saved?.AccountId);
                    ApplyGuess(table, guess, saved?.Mapping ?? guess.Mapping, savedAccount);
                    // The note promises the columns and the account: not for a deleted account or a header-less file (no account)
                    UsesSavedMapping = savedAccount != null;
                }
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

        /// <summary>An OFX / QFX / QIF file: its movements go straight to the preview, no columns to choose.</summary>
        private async Task OpenStatementFileAsync(string fileName, Stream stream, bool isQif)
        {
            var (statement, qif, formats) = await Task.Run(() =>
            {
                if (!isQif)
                    return (OfxParser.Read(stream), (QifParser?)null, (List<StatementDate>)[]);
                var qif = QifParser.Read(stream);
                var formats = qif.DateFormats;
                return (formats.Count == 0 ? new ParsedStatement([]) : qif.ToStatement(formats[0]), qif, formats);
            });
            if (statement.Entries.Count == 0)
            {
                IsBusy = false;
                await _dialogService.AlertAsync(AppResources.ImportFailedToRead, AppResources.ImportNoTable);
                return;
            }

            _table = null;
            _mapping = null;
            _guess = null;
            _preset = null;
            PresetName = string.Empty;
            HasCategoryColumn = false;
            _statement = statement;
            _qif = qif;
            _applyingGuess = true;
            try
            {
                ShowsMapping = false;
                IsQif = isQif;
                DateFormats = formats;
                SelectedDateFormat = DateFormats.FirstOrDefault();
            }
            finally
            {
                _applyingGuess = false;
            }
            UsesSavedMapping = false;
            FileSummary = $"{fileName} · {Plurals.Rows(statement.Entries.Count)}";
            HasFile = true;
            LoadPreviewCommand.Execute(null);
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

        partial void OnSelectedAccountChanged(Account? value)
        {
            if (!_applyingGuess)
                UsesSavedMapping = false; // not the saved account any more
            Reload();
        }

        partial void OnSelectedDescriptionColumnChanged(ColumnOption? value) =>
            Update(m => value == null ? m : m with { DescriptionColumn = value.Index });

        partial void OnSelectedAmountColumnChanged(ColumnOption? value) =>
            Update(m => value == null ? m : m.WithAmountColumn(_table!, value.Index));

        partial void OnSelectedDateFormatChanged(StatementDate? value)
        {
            if (_qif != null)
            {
                if (!_applyingGuess && value != null)
                {
                    _statement = _qif.ToStatement(value);
                    Reload();
                }
                return;
            }
            Update(m => value == null ? m : m with { DateFormat = value });
        }

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
            if (SelectedAccount == null || _table == null && _statement == null)
                return;

            try
            {
                IsBusy = true;
                var rows = _statement != null
                    ? await _importService.PreviewAsync(_statement, SelectedAccount, SelectedExpenseCategory, SelectedIncomeCategory)
                    : await _importService.PreviewAsync(_table!, _mapping!, SelectedAccount,
                        SelectedExpenseCategory, SelectedIncomeCategory, CreateMissingCategories);
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

            // The next file from this bank starts with these choices; a failure here must not cost the Undo dialog
            try
            {
                if (_table != null && _mapping != null && _preset == null)
                    _mappingStore.Remember(_table, _mapping, account.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not save the import mapping");
            }

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
