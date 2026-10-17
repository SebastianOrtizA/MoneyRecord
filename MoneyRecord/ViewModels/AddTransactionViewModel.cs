using MoneyRecord.Helpers;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    [QueryProperty(nameof(TransactionType), "TransactionType")]
    [QueryProperty(nameof(Transaction), "Transaction")]
    public partial class AddTransactionViewModel : ObservableObject
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IBalanceService _balanceService;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IBudgetRepository _budgetRepository;
        private readonly IRecurringTransactionRepository _recurringTransactionRepository;
        private readonly INavigationService _navigationService;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;
        private readonly IAppPaths _paths;
        private readonly IEntryDefaultsService _entryDefaults;
        private readonly IReminderService _reminders;
        private readonly IExchangeRateService _exchangeRates;
        private readonly IConversionService _conversion;
        private readonly IPayeeService _payeeService;
        private readonly ISplitService _splitService;
        private readonly ILogger<AddTransactionViewModel> _logger;

        // Every payee with its use: the suggestions under the payee field
        private List<PayeeSummary> _payees = [];

        // The user tapped a category in this form (or is editing a saved entry): a typed payee no longer changes it
        private bool _categoryChosen;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanSplit))]
        private TransactionType transactionType;

        [ObservableProperty]
        private Transaction? transaction;

        [ObservableProperty]
        private DateTime selectedDate = DateTime.Now;

        [ObservableProperty]
        private string description = string.Empty;

        [ObservableProperty]
        private string amount = string.Empty;

        /// <summary>The payee's name as typed; empty = none. A new name becomes a payee on save.</summary>
        [ObservableProperty]
        private string payeeText = string.Empty;

        /// <summary>Known payees matching <see cref="PayeeText"/> (<see cref="PayeeNames.Suggest"/>).</summary>
        [ObservableProperty]
        private List<PayeeSummary> payeeSuggestions = [];

        partial void OnPayeeTextChanged(string value)
        {
            PayeeSuggestions = PayeeNames.Suggest(_payees, value);
            // A known payee typed in full (or taken from the keyboard's suggestions) picks its category too, like a chip
            if (!_categoryChosen && PayeeNames.Find(_payees, value) is { } payee)
                _ = SelectLastCategoryAsync(payee);
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DescriptionPlaceholder))]
        private Category? selectedCategory;

        /// <summary>The category grid: recently used first (<see cref="IEntryDefaultsService.OrderByRecent"/>).</summary>
        [ObservableProperty]
        private List<CategoryChoice> categoryChoices = [];

        /// <summary>The categories a split part can take (the grid's, in its order).</summary>
        private List<Category> SplitCategories => CategoryChoices.Select(c => c.Category).ToList();

        /// <summary>Only an expense can be split across categories.</summary>
        public bool CanSplit => TransactionType == TransactionType.Expense;

        /// <summary>The payment is split across categories: the parts replace the category grid.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanMakeRecurring))]
        [NotifyPropertyChangedFor(nameof(DescriptionPlaceholder))]
        private bool isSplit;

        public ObservableCollection<SplitPartViewModel> SplitParts { get; } = [];

        /// <summary>"Make recurring" is offered for a new entry in one category (recurring transactions aren't split).</summary>
        public bool CanMakeRecurring => !IsEditMode && !IsSplit;

        /// <summary>A part can be removed while there are more than two (fewer is no split: switch it off instead).</summary>
        public bool CanRemoveSplitParts => SplitParts.Count > 2;

        /// <summary>"Left to assign: $20.00", "Over the amount by $5.00" or "Fully assigned".</summary>
        [ObservableProperty]
        private string splitRemainingText = string.Empty;

        /// <summary>An empty description is saved as the category's name; a split part uses its own.</summary>
        public string DescriptionPlaceholder => IsSplit ? AppResources.SplitDescriptionPlaceholder : SelectedCategory?.Name ?? string.Empty;

        // The rows of the split being edited (replaced on save); empty when it isn't one
        private List<Transaction> _editedParts = [];

        /// <summary>Description, tags, receipt and recurrence sit under "More details".</summary>
        [ObservableProperty]
        private bool isMoreDetailsOpen;

        [ObservableProperty]
        private Account? selectedAccount;

        [ObservableProperty]
        private List<Account> accounts = new();

        [ObservableProperty]
        private string title = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanMakeRecurring))]
        private bool isEditMode = false;

        [ObservableProperty]
        private bool showBudgetWarning = false;

        [ObservableProperty]
        private string budgetWarningText = string.Empty;

        [ObservableProperty]
        private bool isRecurring;

        [ObservableProperty]
        private RecurrenceFrequency selectedFrequency = RecurrenceFrequency.Monthly;

        [ObservableProperty]
        private bool hasEndDate;

        [ObservableProperty]
        private DateTime endDate = DateTime.Now.AddMonths(6);

        [ObservableProperty]
        private bool useSpecificDay;

        [ObservableProperty]
        private int selectedDayOfMonth = 1;

        public List<int> DaysOfMonth { get; } = Enumerable.Range(1, 31).ToList();

        public List<RecurrenceFrequencyItem> Frequencies { get; } =
        [
            new() { Frequency = RecurrenceFrequency.Daily, Display = AppResources.FrequencyDaily },
            new() { Frequency = RecurrenceFrequency.Weekly, Display = AppResources.FrequencyWeekly },
            new() { Frequency = RecurrenceFrequency.Biweekly, Display = AppResources.FrequencyBiweekly },
            new() { Frequency = RecurrenceFrequency.Monthly, Display = AppResources.FrequencyMonthly },
            new() { Frequency = RecurrenceFrequency.Yearly, Display = AppResources.FrequencyYearly },
        ];

        [ObservableProperty]
        private RecurrenceFrequencyItem? selectedFrequencyItem;

        [ObservableProperty]
        private string tagsText = string.Empty;

        /// <summary>The user's own conversion ("Amount in USD"), offered when the account's currency isn't the base.</summary>
        [ObservableProperty]
        private string convertedAmount = string.Empty;

        [ObservableProperty]
        private bool canConvert;

        [ObservableProperty]
        private string baseCurrencyCode = MoneyFormatter.BaseCurrency;

        [ObservableProperty]
        private string convertedAmountLabel = string.Empty;

        [ObservableProperty]
        private string convertedAmountHint = string.Empty;

        /// <summary>"Rate: 1 USD = 3,186.13 COP" from the two amounts; empty until both are entered.</summary>
        [ObservableProperty]
        private string convertedRateText = string.Empty;

        [ObservableProperty]
        private string? receiptPhotoPath;

        [ObservableProperty]
        private bool hasReceipt;

        private readonly ReceiptDraft _receipt;

        [ObservableProperty]
        private bool isMonthlyFrequency;

        partial void OnSelectedFrequencyItemChanged(RecurrenceFrequencyItem? value)
        {
            IsMonthlyFrequency = value?.Frequency == RecurrenceFrequency.Monthly;
            if (!IsMonthlyFrequency)
                UseSpecificDay = false;
        }

        public AddTransactionViewModel(IAccountRepository accountRepository, ICategoryRepository categoryRepository, IBalanceService balanceService, ITransactionRepository transactionRepository, IBudgetRepository budgetRepository, IRecurringTransactionRepository recurringTransactionRepository, INavigationService navigationService, IErrorHandler errorHandler, IDialogService dialogService, IAppPaths paths, IEntryDefaultsService entryDefaults, IReminderService reminders, IExchangeRateService exchangeRates, IConversionService conversion, IPayeeService payeeService, ISplitService splitService, ILogger<AddTransactionViewModel> logger)
        {
            _accountRepository = accountRepository;
            _categoryRepository = categoryRepository;
            _balanceService = balanceService;
            _transactionRepository = transactionRepository;
            _budgetRepository = budgetRepository;
            _recurringTransactionRepository = recurringTransactionRepository;
            _navigationService = navigationService;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
            _paths = paths;
            _entryDefaults = entryDefaults;
            _reminders = reminders;
            _exchangeRates = exchangeRates;
            _conversion = conversion;
            _payeeService = payeeService;
            _splitService = splitService;
            SplitParts.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(CanRemoveSplitParts));
                UpdateSplitRemaining();
            };
            _logger = logger;
            _receipt = new ReceiptDraft(paths.ReceiptsFolder);
            selectedFrequencyItem = Frequencies[3];
        }

        /// <summary>
        /// Resets the ViewModel state for a fresh start
        /// </summary>
        private void ResetState()
        {
            _receipt.Load(null);
            Transaction = null;
            TransactionType = TransactionType.Expense;
            SelectedDate = DateTime.Now;
            Description = string.Empty;
            Amount = string.Empty;
            _payees = [];
            _categoryChosen = false;
            PayeeText = string.Empty;
            SelectedCategory = null;
            SelectedAccount = null;
            Title = AppResources.AddTransaction;
            IsEditMode = false;
            IsMoreDetailsOpen = false;
            TagsText = string.Empty;
            ConvertedAmount = string.Empty;
            // The base currency can't change while the form is open (it is set in Settings)
            BaseCurrencyCode = MoneyFormatter.BaseCurrency;
            ConvertedAmountLabel = string.Format(AppResources.AmountInCurrency, BaseCurrencyCode);
            ConvertedAmountHint = string.Format(AppResources.ConvertedAmountHint, BaseCurrencyCode);
            ShowReceipt();
            IsRecurring = false;
            SelectedFrequencyItem = Frequencies[3];
            HasEndDate = false;
            EndDate = DateTime.Now.AddMonths(6);
            UseSpecificDay = false;
            SelectedDayOfMonth = 1;
            _editedParts = [];
            // Parts first: switching the split off then has nothing to carry back into the category
            ClearSplitParts();
            IsSplit = false;
        }

        /// <summary>Editing a split payment: the whole payment (its total, shared fields and every part) fills the form.</summary>
        private async Task LoadSplitAsync(string splitId)
        {
            _editedParts = await _transactionRepository.GetSplitPartsAsync(splitId);
            if (_editedParts.Count == 0)
                return;

            var names = SplitCategories.ToDictionary(c => c.Id, c => c.Name);
            // A typed description is saved on every part; automatic ones differ per category (or name a since renamed one)
            var descriptions = _editedParts.Select(p => p.Description).Distinct().ToList();
            Description = descriptions.Count == 1
                ? EntryDescription.ForEditing(descriptions[0], names.GetValueOrDefault(_editedParts[0].CategoryId))
                : string.Empty;
            Amount = Money.FromCents(_editedParts.Sum(p => p.AmountCents)).ToString();
            ConvertedAmount = CanConvert && _editedParts.All(p => p.ConvertedAmountCents != null
                    && MoneyFormatter.SameCurrency(p.ConvertedCurrencyCode, BaseCurrencyCode))
                ? Money.FromCents(_editedParts.Sum(p => p.ConvertedAmountCents!.Value)).ToString()
                : string.Empty;
            IsMoreDetailsOpen = Description.Length > 0 || Transaction?.HasTags == true || Transaction?.HasReceipt == true
                || ConvertedAmount.Length > 0;

            foreach (var part in _editedParts)
                AddSplitPart(SplitCategories.FirstOrDefault(c => c.Id == part.CategoryId), Money.FromCents(part.AmountCents).ToString());
            IsSplit = true;
        }

        partial void OnIsSplitChanged(bool value)
        {
            if (value)
            {
                // A split never repeats (recurring transactions stay single-category) and has no single budget to warn about
                IsRecurring = false;
                ShowBudgetWarning = false;
                if (SplitParts.Count == 0)
                {
                    // The chosen category starts the first part, with the whole amount until the user divides it
                    AddSplitPart(SelectedCategory, Amount);
                    AddSplitPart(null, string.Empty);
                }
            }
            else
            {
                // Back to one category: the first part's, if it has one
                if (SplitParts.FirstOrDefault()?.Category is { } first)
                    SelectedCategory = CategoryChoices.FirstOrDefault(c => c.Category.Id == first.Id)?.Category;
                ClearSplitParts();
                _ = CheckBudgetAsync();
            }
        }

        [RelayCommand]
        private void AddSplitPart() => AddSplitPart(null, string.Empty);

        [RelayCommand]
        private void RemoveSplitPart(SplitPartViewModel part)
        {
            if (!CanRemoveSplitParts)
                return;
            part.Changed -= OnSplitPartChanged;
            SplitParts.Remove(part);
        }

        private void AddSplitPart(Category? category, string amount)
        {
            var part = new SplitPartViewModel(SplitCategories) { Category = category, Amount = amount, CurrencyCode = SelectedAccount?.CurrencyCode ?? string.Empty };
            part.Changed += OnSplitPartChanged;
            SplitParts.Add(part);
        }

        private void ClearSplitParts()
        {
            foreach (var part in SplitParts)
                part.Changed -= OnSplitPartChanged;
            SplitParts.Clear();
        }

        private void OnSplitPartChanged(object? sender, EventArgs e) => UpdateSplitRemaining();

        private void UpdateSplitRemaining()
        {
            if (!IsSplit && SplitParts.Count == 0)
            {
                SplitRemainingText = string.Empty;
                return;
            }

            var currency = SelectedAccount?.CurrencyCode;
            var remaining = Money.FromCents(SplitAllocation.Remaining(Money.ToCents(AmountParser.Parse(Amount, currency)),
                SplitParts.Select(p => ParsePartCents(p, currency))));
            SplitRemainingText = remaining switch
            {
                > 0 => string.Format(AppResources.SplitLeftToAssign, MoneyFormatter.Format(remaining, currency)),
                < 0 => string.Format(AppResources.SplitOverBy, MoneyFormatter.Format(-remaining, currency)),
                _ => AppResources.SplitFullyAssigned
            };
        }

        /// <summary>The part's amount in cents; null while it is empty.</summary>
        private static long? ParsePartCents(SplitPartViewModel part, string? currency) =>
            string.IsNullOrWhiteSpace(part.Amount) ? null : Money.ToCents(AmountParser.Parse(part.Amount, currency));

        public async Task InitializeAsync()
        {
            // Save navigation parameters before reset
            var savedTransaction = Transaction;
            var savedType = TransactionType;
            
            // Reset state to handle ViewModel reuse
            ResetState();
            
            // Restore navigation parameters
            Transaction = savedTransaction;
            TransactionType = savedType;

            if (Transaction != null)
            {
                // Edit mode
                IsEditMode = true;
                _categoryChosen = true;
                TransactionType = Transaction.Type;
                await LoadAccountsAsync();
                await LoadCategoriesAsync();
                await LoadPayeesAsync();
                PayeeText = _payees.FirstOrDefault(p => p.Id == Transaction.PayeeId)?.Name ?? string.Empty;
                SelectedDate = Transaction.Date;
                Description = Transaction.Description;
                TagsText = Transaction.Tags ?? string.Empty;
                _receipt.Load(Transaction.ReceiptPhotoPath);
                ShowReceipt();

                Title = TransactionType == TransactionType.Income ? AppResources.EditIncome : AppResources.EditExpense;
                
                SelectedCategory = CategoryChoices.FirstOrDefault(c => c.Category.Id == Transaction.CategoryId)?.Category;

                // An automatic description (the category name) shows as the placeholder, so it follows a category change
                Description = EntryDescription.ForEditing(Transaction.Description, SelectedCategory?.Name);

                // Open the details when they hold something beyond the defaults
                IsMoreDetailsOpen = Description.Length > 0 || Transaction.HasTags || Transaction.HasReceipt
                    || (Transaction.ConvertedAmount != null && MoneyFormatter.SameCurrency(Transaction.ConvertedCurrencyCode, BaseCurrencyCode));
                
                // Select the current account
                if (Transaction.AccountId.HasValue)
                {
                    SelectedAccount = Accounts.FirstOrDefault(a => a.Id == Transaction.AccountId.Value);
                }
                else
                {
                    SelectedAccount = Accounts.FirstOrDefault(a => a.IsDefault);
                }

                // After the account: the amount field formats in the account's currency
                Amount = Transaction.Amount.ToString();
                if (Transaction.ConvertedAmount is { } converted && CanConvert
                    && MoneyFormatter.SameCurrency(Transaction.ConvertedCurrencyCode, BaseCurrencyCode))
                    ConvertedAmount = converted.ToString();

                if (Transaction.SplitId is { Length: > 0 } splitId)
                    await LoadSplitAsync(splitId);
            }
            else
            {
                // Add mode
                IsEditMode = false;
                Title = TransactionType == TransactionType.Income ? AppResources.AddIncome : AppResources.AddExpense;
                await LoadAccountsAsync();
                await LoadCategoriesAsync();
                await LoadPayeesAsync();

                // The account and category last used for this type
                SelectedAccount = _entryDefaults.DefaultAccount(TransactionType, Accounts);
                SelectedCategory = _entryDefaults.DefaultCategory(TransactionType, CategoryChoices.Select(c => c.Category));
            }
        }

        private async Task LoadAccountsAsync()
        {
            Accounts = await _accountRepository.GetOwnAsync();
        }

        private async Task LoadCategoriesAsync()
        {
            var categoryType = TransactionType == TransactionType.Income ? CategoryType.Income : CategoryType.Expense;
            var categories = await _categoryRepository.GetByTypeAsync(categoryType);
            CategoryChoices = _entryDefaults.OrderByRecent(TransactionType, categories)
                .Select(c => new CategoryChoice(c))
                .ToList();
        }

        private async Task LoadPayeesAsync()
        {
            _payees = await _payeeService.GetAllAsync();
        }

        /// <summary>A suggestion tapped: its name, and the category its last entry of this type had (even over a chosen one).</summary>
        [RelayCommand]
        private void SelectPayee(PayeeSummary payee)
        {
            // An explicit pick: the payee's category wins, and typing the name in full is handled the same way
            _categoryChosen = false;
            PayeeText = payee.Name;
        }

        /// <summary>Selects the category of the payee's latest entry of this type, when it is offered and still wanted.</summary>
        private async Task SelectLastCategoryAsync(PayeeSummary payee)
        {
            try
            {
                var categoryId = await _payeeService.GetLastCategoryAsync(payee.Id, TransactionType);
                // Meanwhile the user may have picked a category or typed another payee
                if (_categoryChosen || IsSplit || !PayeeNames.Same(PayeeText, payee.Name))
                    return;
                if (CategoryChoices.FirstOrDefault(c => c.Category.Id == categoryId) is { } choice)
                    SelectedCategory = choice.Category;
            }
            catch (Exception ex)
            {
                // Only a convenience: the user picks the category themselves
                _logger.LogWarning(ex, "Could not look up the payee's last category");
            }
        }

        [RelayCommand]
        private void SelectCategory(CategoryChoice choice)
        {
            _categoryChosen = true;
            SelectedCategory = choice.Category;
        }

        [RelayCommand]
        private void ToggleMoreDetails() => IsMoreDetailsOpen = !IsMoreDetailsOpen;

        partial void OnSelectedAccountChanged(Account? value)
        {
            CanConvert = value != null && !MoneyFormatter.SameCurrency(value.CurrencyCode, BaseCurrencyCode);
            UpdateConvertedRate();
            foreach (var part in SplitParts)
                part.CurrencyCode = value?.CurrencyCode ?? string.Empty;
            UpdateSplitRemaining();
        }

        partial void OnAmountChanged(string value)
        {
            UpdateConvertedRate();
            UpdateSplitRemaining();
        }

        partial void OnConvertedAmountChanged(string value) => UpdateConvertedRate();

        private void UpdateConvertedRate()
        {
            var rate = CanConvert
                ? TransferRate.Describe(AmountParser.Parse(Amount, SelectedAccount?.CurrencyCode), SelectedAccount?.CurrencyCode,
                    AmountParser.Parse(ConvertedAmount, BaseCurrencyCode), BaseCurrencyCode)
                : null;
            ConvertedRateText = rate == null ? string.Empty : string.Format(AppResources.ExchangeRateValue, rate);
        }

        partial void OnSelectedCategoryChanged(Category? value)
        {
            foreach (var choice in CategoryChoices)
                choice.IsSelected = choice.Category.Id == value?.Id;

            _ = CheckBudgetAsync();
        }

        private async Task CheckBudgetAsync()
        {
            var category = SelectedCategory;
            if (TransactionType != TransactionType.Expense || category == null || IsSplit)
            {
                ShowBudgetWarning = false;
                return;
            }

            try
            {
                var budget = await _budgetRepository.GetByCategoryIdAsync(category.Id);
                var spent = 0m;
                if (budget != null)
                {
                    var (startDate, endDate) = DateRangeHelper.GetBudgetWindow(budget.Period, DateTime.Now);
                    // What counts against the (base currency) limit: converted spending, or only the base currency's
                    spent = (await _conversion.GetExpensesByCategoryAsync(startDate, endDate)).GetValueOrDefault(category.Id)?.Counted ?? 0;
                }

                // Another category was picked meanwhile (its own check shows its warning), or the payment was split
                if (category != SelectedCategory || IsSplit)
                    return;

                if (budget == null)
                {
                    ShowBudgetWarning = false;
                    return;
                }

                var remaining = budget.LimitAmount - spent;

                if (remaining <= 0)
                {
                    BudgetWarningText = string.Format(AppResources.BudgetExceeded, category.Name, MoneyFormatter.Format(spent), MoneyFormatter.Format(budget.LimitAmount));
                    ShowBudgetWarning = true;
                }
                else if (remaining < budget.LimitAmount * 0.2m)
                {
                    BudgetWarningText = string.Format(AppResources.BudgetNearLimit, category.Name, MoneyFormatter.Format(remaining));
                    ShowBudgetWarning = true;
                }
                else
                {
                    ShowBudgetWarning = false;
                }
            }
            catch
            {
                ShowBudgetWarning = false;
            }
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            var amountValue = AmountParser.Parse(Amount, SelectedAccount?.CurrencyCode);
            if (amountValue <= 0 || !Money.IsInRange(amountValue))
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterValidAmount);
                return;
            }

            if (SelectedAccount == null)
            {
                await _errorHandler.HandleAsync(AppResources.PleaseSelectAccount);
                return;
            }

            List<SplitPart>? parts = null;
            if (IsSplit)
            {
                parts = await BuildSplitPartsAsync(amountValue);
                if (parts == null)
                    return;
            }
            else if (SelectedCategory == null)
            {
                await _errorHandler.HandleAsync(AppResources.PleaseSelectCategory);
                return;
            }

            // The optional "Amount in USD": empty = the downloaded rate
            decimal? convertedValue = null;
            if (CanConvert && !string.IsNullOrWhiteSpace(ConvertedAmount))
            {
                convertedValue = AmountParser.Parse(ConvertedAmount, BaseCurrencyCode);
                if (convertedValue <= 0 || !Money.IsInRange(convertedValue.Value))
                {
                    await _errorHandler.HandleAsync(AppResources.PleaseEnterValidAmount);
                    return;
                }
            }

            int accountId = SelectedAccount.Id;

            // Validate balance for expense transactions if account doesn't allow negative balance
            if (TransactionType == TransactionType.Expense && !SelectedAccount.AllowNegativeBalance)
            {
                var currentBalance = await _balanceService.GetAccountBalanceAsync(accountId);

                decimal adjustedBalance = currentBalance;
                if (IsEditMode && Transaction != null && Transaction.Type == TransactionType.Expense)
                {
                    // What the edited entry took out: a split payment's whole total
                    adjustedBalance += _editedParts.Count > 0 ? Money.FromCents(_editedParts.Sum(p => p.AmountCents)) : Transaction.Amount;
                }

                if (adjustedBalance - amountValue < 0)
                {
                    await _errorHandler.HandleAsync(string.Format(AppResources.InsufficientAccountBalance, SelectedAccount.Name));
                    return;
                }
            }

            // After every check, so a refused save never leaves a new payee behind
            var payeeId = await _payeeService.ResolveAsync(PayeeText);

            // A split payment, or an edited one put back into one category, is replaced as a whole
            if (parts != null || _editedParts.Count > 0)
            {
                parts ??= [new SplitPart(SelectedCategory!.Id, Money.ToCents(amountValue), EntryDescription.Resolve(Description, SelectedCategory.Name))];
                await SaveSplitAsync(parts, accountId, payeeId, convertedValue);
            }
            else if (IsEditMode && Transaction != null)
            {
                var savedDescription = EntryDescription.Resolve(Description, SelectedCategory!.Name);

                // Update existing transaction
                Transaction.Date = SelectedDate;
                Transaction.Description = savedDescription;
                Transaction.Amount = amountValue;
                Transaction.CategoryId = SelectedCategory.Id;
                Transaction.Type = TransactionType;
                Transaction.AccountId = accountId;
                Transaction.PayeeId = payeeId;
                Transaction.Tags = string.IsNullOrWhiteSpace(TagsText) ? null : TagsText.Trim();
                Transaction.ReceiptPhotoPath = ReceiptPhotoPath;
                Transaction.SetConversion(convertedValue, SelectedAccount.CurrencyCode, BaseCurrencyCode);

                await _transactionRepository.SaveAsync(Transaction);
                _receipt.Commit(); // deletes a removed or replaced receipt
                await _dialogService.ToastAsync(AppResources.TransactionUpdatedSuccessfully);
            }
            else
            {
                var savedDescription = EntryDescription.Resolve(Description, SelectedCategory!.Name);

                // Create new transaction
                var newTransaction = new Transaction
                {
                    Date = SelectedDate,
                    Description = savedDescription,
                    Amount = amountValue,
                    CategoryId = SelectedCategory.Id,
                    Type = TransactionType,
                    AccountId = accountId,
                    PayeeId = payeeId,
                    Tags = string.IsNullOrWhiteSpace(TagsText) ? null : TagsText.Trim(),
                    ReceiptPhotoPath = ReceiptPhotoPath
                };
                newTransaction.SetConversion(convertedValue, SelectedAccount.CurrencyCode, BaseCurrencyCode);

                await _transactionRepository.SaveAsync(newTransaction);
                _receipt.Commit();
                _entryDefaults.Remember(TransactionType, accountId, SelectedCategory.Id);

                if (IsRecurring && SelectedFrequencyItem != null)
                {
                    var frequency = SelectedFrequencyItem.Frequency;
                    int? dayOfMonth = (frequency == RecurrenceFrequency.Monthly && UseSpecificDay)
                        ? SelectedDayOfMonth
                        : null;

                    var nextOccurrence = RecurrenceCalculator.Next(SelectedDate, frequency, dayOfMonth);

                    var recurring = new RecurringTransaction
                    {
                        Amount = amountValue,
                        Description = savedDescription,
                        CategoryId = SelectedCategory.Id,
                        AccountId = accountId,
                        PayeeId = payeeId,
                        TransactionType = TransactionType,
                        Frequency = frequency,
                        DayOfMonth = dayOfMonth,
                        StartDate = SelectedDate,
                        EndDate = HasEndDate ? EndDate : null,
                        NextOccurrence = nextOccurrence,
                        LastProcessedDate = SelectedDate,
                        IsActive = true
                    };

                    await _recurringTransactionRepository.SaveAsync(recurring);
                    // A first schedule: offer reminders before its bills come due (once)
                    await _reminders.OfferAsync();
                }
            }

            // An expense may take a budget to 80% or over it
            if (TransactionType == TransactionType.Expense)
                await _reminders.CheckBudgetsAsync();

            // An entry in another currency (perhaps dated before the stored rates) gets its rates now, in the background
            if (CanConvert)
                _ = _exchangeRates.UpdateIfEnabledAsync();

            await _navigationService.GoBackAsync();
        }

        /// <summary>
        /// The split's parts, checked: each has a category and a positive amount, and together they make the total. One part
        /// left empty takes what is left. Null after telling the user what to fix.
        /// </summary>
        private async Task<List<SplitPart>?> BuildSplitPartsAsync(decimal total)
        {
            if (SplitParts.Any(p => p.Category == null))
            {
                await _errorHandler.HandleAsync(AppResources.SplitPartNeedsCategory);
                return null;
            }

            var currency = SelectedAccount?.CurrencyCode;
            var completion = SplitAllocation.Complete(Money.ToCents(total), SplitParts.Select(p => ParsePartCents(p, currency)).ToList());
            if (completion.Cents is not { } cents)
            {
                await _errorHandler.HandleAsync(completion.Problem == SplitProblem.DoesNotAddUp
                    ? string.Format(AppResources.SplitMustAddUp, MoneyFormatter.Format(total, currency))
                    : AppResources.SplitPartNeedsAmount);
                return null;
            }

            return SplitParts
                .Select((p, i) => new SplitPart(p.Category!.Id, cents[i], EntryDescription.Resolve(Description, p.Category.Name)))
                .ToList();
        }

        /// <summary>Saves the payment as its parts, replacing the transaction or split being edited.</summary>
        private async Task SaveSplitAsync(List<SplitPart> parts, int accountId, int? payeeId, decimal? convertedValue)
        {
            var template = new Transaction
            {
                Date = SelectedDate,
                Type = TransactionType,
                AccountId = accountId,
                PayeeId = payeeId,
                Tags = string.IsNullOrWhiteSpace(TagsText) ? null : TagsText.Trim(),
                ReceiptPhotoPath = ReceiptPhotoPath,
                SplitId = _editedParts.FirstOrDefault()?.SplitId
            };
            template.SetConversion(convertedValue, SelectedAccount?.CurrencyCode, BaseCurrencyCode);

            List<int> replaced = _editedParts.Count > 0 ? _editedParts.Select(p => p.Id).ToList()
                : IsEditMode && Transaction != null ? [Transaction.Id]
                : [];
            await _splitService.SaveAsync(template, parts, replaced);
            _receipt.Commit(); // deletes a removed or replaced receipt

            if (IsEditMode)
                await _dialogService.ToastAsync(AppResources.TransactionUpdatedSuccessfully);
            else
                _entryDefaults.Remember(TransactionType, accountId, parts[0].CategoryId);
        }

        [RelayCommand]
        private async Task TakePhotoAsync()
        {
            try
            {
                if (!MediaPicker.Default.IsCaptureSupported)
                {
                    await _errorHandler.HandleAsync(AppResources.CameraNotSupported);
                    return;
                }

                var photo = await MediaPicker.Default.CapturePhotoAsync();
                if (photo != null)
                    await SaveReceiptPhotoAsync(photo);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToTakePhoto);
            }
        }

        [RelayCommand]
        private async Task PickPhotoAsync()
        {
            try
            {
                var photos = await MediaPicker.Default.PickPhotosAsync();
                var photo = photos?.FirstOrDefault();
                if (photo != null)
                    await SaveReceiptPhotoAsync(photo);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToPickPhoto);
            }
        }

        private async Task SaveReceiptPhotoAsync(FileResult photo)
        {
            var receiptsDir = _paths.ReceiptsFolder;
            Directory.CreateDirectory(receiptsDir);

            var fileName = $"receipt_{DateTime.Now:yyyyMMdd_HHmmss_fff}{Path.GetExtension(photo.FileName)}";
            var filePath = Path.Combine(receiptsDir, fileName);

            using (var stream = await photo.OpenReadAsync())
            using (var fileStream = File.Create(filePath))
                await stream.CopyToAsync(fileStream);

            _receipt.Set(filePath);
            ShowReceipt();
        }

        [RelayCommand]
        private void RemovePhoto()
        {
            _receipt.Remove();
            ShowReceipt();
        }

        /// <summary>
        /// Deletes a photo that was never saved (called on cancel and when the page is left without saving).
        /// </summary>
        public void DiscardUnsavedPhoto()
        {
            _receipt.Discard();
            ShowReceipt();
        }

        private void ShowReceipt()
        {
            ReceiptPhotoPath = _receipt.Path;
            HasReceipt = !string.IsNullOrWhiteSpace(ReceiptPhotoPath);
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            DiscardUnsavedPhoto();
            await _navigationService.GoBackAsync();
        }
    }
}

