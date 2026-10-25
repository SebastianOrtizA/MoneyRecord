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
    [QueryProperty(nameof(TypeName), "Type")]
    [QueryProperty(nameof(AccountIdString), "AccountId")]
    [QueryProperty(nameof(ScheduleName), "Schedule")]
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
        private readonly PocketCoverPrompt _pocketCover;
        private readonly ITripService _trips;
        private readonly IReceiptScanner _receiptScanner;
        private readonly TimeProvider _clock;
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

        /// <summary>The type from a route string (the home-screen widget's buttons): "Expense" or "Income".</summary>
        public string? TypeName
        {
            get => TransactionType.ToString();
            set
            {
                if (Enum.TryParse<TransactionType>(value, out var type))
                    TransactionType = type;
            }
        }

        /// <summary>A new entry in this account (the widget's buttons) instead of the last one used.</summary>
        public string? AccountIdString { get; set; }

        /// <summary>"true" from the Recurring page's + button: the form makes a schedule (<see cref="IsScheduleMode"/>).</summary>
        public string? ScheduleName
        {
            get => IsScheduleMode.ToString();
            set => IsScheduleMode = bool.TryParse(value, out var schedule) && schedule;
        }

        /// <summary>
        /// A new schedule from the Recurring page: the date is its first occurrence (tomorrow at the earliest) and only the
        /// schedule is saved. No split, receipt, tags or own conversion: those belong to a recorded entry.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanSplit), nameof(CanScanReceipt), nameof(SavesOnlySchedule), nameof(ShowsEntryDetails), nameof(MinimumDate), nameof(DateLabel))]
        private bool isScheduleMode;

        /// <summary>
        /// The save records nothing now, only the schedule: from the Recurring page, or recurring with a future date (its
        /// first entry is recorded on that date).
        /// </summary>
        public bool SavesOnlySchedule => IsScheduleMode
            || (IsRecurring && CanMakeRecurring && RecurringStart.IsFuture(SelectedDate, Now));

        /// <summary>Tags, the own conversion and the receipt belong to a recorded entry: hidden when only a schedule is saved.</summary>
        public bool ShowsEntryDetails => !SavesOnlySchedule;

        // The clock the balances use (IBalanceService.CountedBefore): one definition of today
        private DateTime Now => _clock.GetLocalNow().DateTime;

        /// <summary>A schedule starts tomorrow at the earliest; an entry can have any date.</summary>
        public DateTime MinimumDate => IsScheduleMode ? RecurringStart.EarliestScheduleDate(Now) : new DateTime(1900, 1, 1);

        public string DateLabel => IsScheduleMode ? AppResources.FirstDate : AppResources.Date;

        /// <summary>
        /// Under a future date: only the schedule is saved (recurring), or the entry counts in balances from that date.
        /// Empty for today or earlier.
        /// </summary>
        public string FutureDateNote => !RecurringStart.IsFuture(SelectedDate, Now)
            ? string.Empty
            : string.Format(SavesOnlySchedule ? AppResources.ScheduleStartsNote : AppResources.FutureDateNote,
                DateFormatter.Format(SelectedDate, DateStyle.Long));

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FutureDateNote), nameof(SavesOnlySchedule), nameof(ShowsEntryDetails))]
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

        /// <summary>
        /// The active trip this expense belongs to or may belong to (a new expense, or editing one of the active trip's); null =
        /// none. The grid then offers its trip categories (as <see cref="Category"/> rows: the IDs are trip category IDs).
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasTrip), nameof(IsTripMode), nameof(TripBannerText), nameof(CanSplit), nameof(CanMakeRecurring))]
        private Trip? tripForEntry;

        /// <summary>"Not part of the trip": the normal categories, and the expense stays out of the trip.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsTripMode), nameof(CanSplit), nameof(CanMakeRecurring))]
        private bool isOutsideTrip;

        public bool HasTrip => TripForEntry != null;

        public bool IsTripMode => TripForEntry != null && !IsOutsideTrip;

        public string TripBannerText => TripForEntry == null ? string.Empty : string.Format(AppResources.TripBanner, TripForEntry.Name);

        /// <summary>"Trip: Lisbon · Lodging" under an expense of an ended trip (read only); empty otherwise.</summary>
        [ObservableProperty]
        private string tripInfoText = string.Empty;

        partial void OnIsOutsideTripChanged(bool value)
        {
            if (_loadingTrip)
                return;
            _categoryChosen = false;
            _ = ReloadCategoriesForTripAsync();
        }

        private bool _loadingTrip;

        // Each toggle starts a reload; a newer one makes an older one that is still waiting give up
        private int _tripReloadVersion;

        private async Task ReloadCategoriesForTripAsync()
        {
            var version = ++_tripReloadVersion;
            try
            {
                await LoadCategoriesAsync();
                if (version != _tripReloadVersion)
                    return;

                SelectedCategory = IsTripMode
                    ? CategoryChoices.FirstOrDefault()?.Category
                    : _entryDefaults.DefaultCategory(TransactionType, CategoryChoices.Select(c => c.Category));
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadTrips);
            }
        }

        // In trip mode (<see cref="IsTripMode"/>) this is a fake Category carrying a TRIP category's ID: every use of its ID
        // (budget check, payee's last category, entry defaults, the saves) must be guarded with IsTripMode.
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DescriptionPlaceholder))]
        private Category? selectedCategory;

        /// <summary>The category grid: recently used first (<see cref="IEntryDefaultsService.OrderByRecent"/>).</summary>
        [ObservableProperty]
        private List<CategoryChoice> categoryChoices = [];

        /// <summary>The categories a split part can take (the grid's, in its order).</summary>
        private List<Category> SplitCategories => CategoryChoices.Select(c => c.Category).ToList();

        /// <summary>Only an expense can be split across categories (recurring transactions aren't split).</summary>
        public bool CanSplit => TransactionType == TransactionType.Expense && !IsScheduleMode && !IsTripMode;

        /// <summary>The payment is split across categories: the parts replace the category grid.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanMakeRecurring), nameof(FutureDateNote), nameof(SavesOnlySchedule), nameof(ShowsEntryDetails))]
        [NotifyPropertyChangedFor(nameof(DescriptionPlaceholder))]
        private bool isSplit;

        public ObservableCollection<SplitPartViewModel> SplitParts { get; } = [];

        /// <summary>"Make recurring" is offered for a new entry in one category (recurring transactions aren't split).</summary>
        public bool CanMakeRecurring => !IsEditMode && !IsSplit && !IsTripMode;

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
        [NotifyPropertyChangedFor(nameof(CanMakeRecurring), nameof(FutureDateNote), nameof(SavesOnlySchedule), nameof(ShowsEntryDetails))]
        private bool isEditMode = false;

        [ObservableProperty]
        private bool showBudgetWarning = false;

        [ObservableProperty]
        private string budgetWarningText = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FutureDateNote), nameof(SavesOnlySchedule), nameof(ShowsEntryDetails))]
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

        /// <summary>A receipt photo can be read on this platform: the scan button beside the amount.</summary>
        public bool CanScanReceipt => _receiptScanner.IsSupported && !IsScheduleMode;

        /// <summary>A new photo's text is being read (its total and date fill in the form).</summary>
        [ObservableProperty]
        private bool isReadingReceipt;

        // Monthly is the default frequency (set as a field, which raises no change)
        [ObservableProperty]
        private bool isMonthlyFrequency = true;

        partial void OnSelectedFrequencyItemChanged(RecurrenceFrequencyItem? value)
        {
            IsMonthlyFrequency = value?.Frequency == RecurrenceFrequency.Monthly;
            if (!IsMonthlyFrequency)
                UseSpecificDay = false;
        }

        public AddTransactionViewModel(IAccountRepository accountRepository, ICategoryRepository categoryRepository, IBalanceService balanceService, ITransactionRepository transactionRepository, IBudgetRepository budgetRepository, IRecurringTransactionRepository recurringTransactionRepository, INavigationService navigationService, IErrorHandler errorHandler, IDialogService dialogService, IAppPaths paths, IEntryDefaultsService entryDefaults, IReminderService reminders, IExchangeRateService exchangeRates, IConversionService conversion, IPayeeService payeeService, ISplitService splitService, PocketCoverPrompt pocketCover, ITripService trips, IReceiptScanner receiptScanner, TimeProvider clock, ILogger<AddTransactionViewModel> logger)
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
            _pocketCover = pocketCover;
            _trips = trips;
            _receiptScanner = receiptScanner;
            _clock = clock;
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
            _loadingTrip = true;
            TripForEntry = null;
            IsOutsideTrip = false;
            TripInfoText = string.Empty;
            _loadingTrip = false;
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
                await LoadTripAsync(Transaction);
                await LoadCategoriesAsync();
                await LoadPayeesAsync();
                PayeeText = _payees.FirstOrDefault(p => p.Id == Transaction.PayeeId)?.Name ?? string.Empty;
                SelectedDate = Transaction.Date;
                Description = Transaction.Description;
                TagsText = Transaction.Tags ?? string.Empty;
                _receipt.Load(Transaction.ReceiptPhotoPath);
                ShowReceipt();

                Title = TransactionType == TransactionType.Income ? AppResources.EditIncome : AppResources.EditExpense;
                
                SelectedCategory = CategoryChoices.FirstOrDefault(c => c.Category.Id == (IsTripMode ? Transaction.TripCategoryId : Transaction.CategoryId))?.Category;

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
                await LoadTripAsync(null);
                await LoadCategoriesAsync();
                await LoadPayeesAsync();

                // The account the widget asked for, else the account and category last used for this type
                SelectedAccount = (int.TryParse(AccountIdString, out var accountId) ? Accounts.FirstOrDefault(a => a.Id == accountId) : null)
                    ?? (IsTripMode ? Accounts.FirstOrDefault(a => a.Id == TripForEntry!.AccountId) : null)
                    ?? _entryDefaults.DefaultAccount(TransactionType, Accounts);
                SelectedCategory = IsTripMode
                    ? CategoryChoices.FirstOrDefault()?.Category
                    : _entryDefaults.DefaultCategory(TransactionType, CategoryChoices.Select(c => c.Category));

                if (IsScheduleMode)
                {
                    Title = TransactionType == TransactionType.Income ? AppResources.NewRecurringIncome : AppResources.NewRecurringExpense;
                    SelectedDate = RecurringStart.EarliestScheduleDate(Now);
                    // The recurrence options sit under "More details"
                    IsRecurring = true;
                    IsMoreDetailsOpen = true;
                }
            }
        }

        private async Task LoadAccountsAsync()
        {
            Accounts = await _accountRepository.GetOwnAsync();
        }

        /// <summary>
        /// Sets the trip the form belongs to: the active one for a new expense (not a schedule), or for an edited expense of it;
        /// an expense of an ended trip shows its trip read only.
        /// </summary>
        private async Task LoadTripAsync(Transaction? edited)
        {
            _loadingTrip = true;
            try
            {
                var active = TransactionType == TransactionType.Expense && !IsScheduleMode ? await _trips.GetActiveAsync() : null;
                TripForEntry = edited == null ? active : edited.TripId is { } id && active?.Id == id ? active : null;
                IsOutsideTrip = false;
                TripInfoText = string.Empty;
                if (edited?.TripId is { } tripId && TripForEntry == null && await _trips.GetAsync(tripId) is { } trip)
                {
                    var category = (await _trips.GetCategoriesAsync()).FirstOrDefault(c => c.Id == edited.TripCategoryId);
                    TripInfoText = category == null ? string.Format(AppResources.TripInfoOnly, trip.Name) : string.Format(AppResources.TripInfo, trip.Name, category.Name);
                }
            }
            finally
            {
                _loadingTrip = false;
            }
        }

        private async Task LoadCategoriesAsync()
        {
            var categoryType = TransactionType == TransactionType.Income ? CategoryType.Income : CategoryType.Expense;
            if (IsTripMode)
            {
                CategoryChoices = (await _trips.GetCategoriesAsync())
                    .Select(c => new CategoryChoice(new Category { Id = c.Id, Name = c.Name, IconCode = c.IconCode, Type = CategoryType.Expense }))
                    .ToList();
                return;
            }

            var categories = await _categoryRepository.GetByTypeAsync(categoryType);
            // "Travel" holds trip expenses only: never picked for one that is not part of a trip
            categories = categories.Where(c => !c.IsTravel).ToList();
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
                if (_categoryChosen || IsSplit || IsTripMode || !PayeeNames.Same(PayeeText, payee.Name))
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
            if (TransactionType != TransactionType.Expense || category == null || IsSplit || IsTripMode)
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

            // A recurring entry dated in the future saves only its schedule: nothing is recorded (or counted) before then
            var start = IsRecurring && CanMakeRecurring && SelectedFrequencyItem != null
                ? RecurringStart.For(SelectedDate, Now, SelectedFrequencyItem.Frequency, DayOfMonthChoice)
                : null;
            var schedulesOnly = start is { RecordsFirstNow: false };
            if (IsScheduleMode && !schedulesOnly)
            {
                await _errorHandler.HandleAsync(AppResources.ScheduleDateMustBeFuture);
                return;
            }

            // Validate balance for expense transactions if account doesn't allow negative balance (nothing leaves it yet
            // for a schedule)
            if (TransactionType == TransactionType.Expense && !SelectedAccount.AllowNegativeBalance && !schedulesOnly)
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

            // An expense that takes money set aside in pockets: ask which pocket to take it from (a schedule takes nothing yet)
            var cover = PocketCoverChoice.KeepAsIs;
            if (TransactionType == TransactionType.Expense && !schedulesOnly)
            {
                cover = await _pocketCover.AskAsync(accountId, ExtraOutflowOn(accountId, amountValue), SelectedDate);
                if (!cover.Proceed)
                    return;
            }

            // After every check, so a refused save never leaves a new payee behind
            var payeeId = await _payeeService.ResolveAsync(PayeeText);

            if (schedulesOnly)
            {
                await SaveRecurringAsync(start!, amountValue, EntryDescription.Resolve(Description, SelectedCategory!.Name), accountId, payeeId);
                // A receipt photo belongs to a recorded entry
                DiscardUnsavedPhoto();
                _entryDefaults.Remember(TransactionType, accountId, SelectedCategory.Id);
                await _dialogService.ToastAsync(AppResources.RecurringScheduled);
                await _navigationService.GoBackAsync();
                return;
            }

            int? savedTransactionId = null;
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
                Transaction.CategoryId = await CategoryIdToSaveAsync();
                if (TripForEntry != null)
                {
                    // "Not part of the trip" takes an expense of the active trip out of it
                    Transaction.TripId = IsTripMode ? TripForEntry.Id : null;
                    Transaction.TripCategoryId = IsTripMode ? SelectedCategory.Id : null;
                }

                Transaction.Type = TransactionType;
                Transaction.AccountId = accountId;
                Transaction.PayeeId = payeeId;
                Transaction.Tags = string.IsNullOrWhiteSpace(TagsText) ? null : TagsText.Trim();
                Transaction.ReceiptPhotoPath = ReceiptPhotoPath;
                Transaction.SetConversion(convertedValue, SelectedAccount.CurrencyCode, BaseCurrencyCode);

                await _transactionRepository.SaveAsync(Transaction);
                savedTransactionId = Transaction.Id;
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
                    CategoryId = await CategoryIdToSaveAsync(),
                    TripId = IsTripMode ? TripForEntry!.Id : null,
                    TripCategoryId = IsTripMode ? SelectedCategory.Id : null,
                    Type = TransactionType,
                    AccountId = accountId,
                    PayeeId = payeeId,
                    Tags = string.IsNullOrWhiteSpace(TagsText) ? null : TagsText.Trim(),
                    ReceiptPhotoPath = ReceiptPhotoPath
                };
                newTransaction.SetConversion(convertedValue, SelectedAccount.CurrencyCode, BaseCurrencyCode);

                await _transactionRepository.SaveAsync(newTransaction);
                savedTransactionId = newTransaction.Id;
                _receipt.Commit();
                if (!IsTripMode)
                    _entryDefaults.Remember(TransactionType, accountId, SelectedCategory.Id);

                if (start != null)
                    await SaveRecurringAsync(start, amountValue, savedDescription, accountId, payeeId);
            }

            await _pocketCover.RecordAsync(accountId, cover, SelectedDate, transactionId: savedTransactionId);

            // An expense may take a budget to 80% or over it
            if (TransactionType == TransactionType.Expense)
                await _reminders.CheckBudgetsAsync();

            // An entry in another currency (perhaps dated before the stored rates) gets its rates now, in the background
            if (CanConvert)
                _ = _exchangeRates.UpdateIfEnabledAsync();

            await _navigationService.GoBackAsync();
        }

        /// <summary>What saving takes out of <paramref name="accountId"/> on top of what the entry being edited took already.</summary>
        private decimal ExtraOutflowOn(int accountId, decimal amount) =>
            IsEditMode && Transaction is { Type: TransactionType.Expense } original
                ? PocketCoverPrompt.ExtraOutflow(amount,
                    _editedParts.Count > 0 ? Money.FromCents(_editedParts.Sum(p => p.AmountCents)) : original.Amount,
                    original.Date, _balanceService.CountedBefore, original.AccountId == accountId)
                : amount;

        /// <summary>The ledger category to save: "Travel" while the expense is part of the active trip, else the picked one.</summary>
        private async Task<int> CategoryIdToSaveAsync() => IsTripMode ? await _trips.GetTravelCategoryIdAsync() : SelectedCategory!.Id;

        /// <summary>The chosen day of the month: only for a monthly recurrence with "Specific day" on.</summary>
        private int? DayOfMonthChoice =>
            SelectedFrequencyItem?.Frequency == RecurrenceFrequency.Monthly && UseSpecificDay ? SelectedDayOfMonth : null;

        /// <summary>Saves the new recurring transaction's schedule, from <paramref name="start"/>.</summary>
        private async Task SaveRecurringAsync(RecurringStart start, decimal amount, string description, int accountId, int? payeeId)
        {
            await _recurringTransactionRepository.SaveAsync(new RecurringTransaction
            {
                Amount = amount,
                Description = description,
                CategoryId = SelectedCategory!.Id,
                AccountId = accountId,
                PayeeId = payeeId,
                TransactionType = TransactionType,
                Frequency = SelectedFrequencyItem!.Frequency,
                DayOfMonth = DayOfMonthChoice,
                StartDate = SelectedDate,
                EndDate = HasEndDate ? EndDate : null,
                NextOccurrence = start.NextOccurrence,
                LastProcessedDate = start.LastProcessedDate,
                IsActive = true
            });
            // A first schedule: offer reminders before its bills come due (once)
            await _reminders.OfferAsync();
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
            string? path = null;
            try
            {
                if (!MediaPicker.Default.IsCaptureSupported)
                {
                    await _errorHandler.HandleAsync(AppResources.CameraNotSupported);
                    return;
                }

                var photo = await MediaPicker.Default.CapturePhotoAsync();
                if (photo != null)
                    path = await SaveReceiptPhotoAsync(photo);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToTakePhoto);
            }
            // After the photo is in: a failed reading is not a failed photo
            if (path != null)
                await ReadReceiptAsync(path);
        }

        [RelayCommand]
        private async Task PickPhotoAsync()
        {
            string? path = null;
            try
            {
                var photos = await MediaPicker.Default.PickPhotosAsync();
                var photo = photos?.FirstOrDefault();
                if (photo != null)
                    path = await SaveReceiptPhotoAsync(photo);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToPickPhoto);
            }
            if (path != null)
                await ReadReceiptAsync(path);
        }

        /// <summary>Copies the photo into the receipts folder and shows it; returns its path.</summary>
        private async Task<string> SaveReceiptPhotoAsync(FileResult photo)
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
            return filePath;
        }

        /// <summary>The scan button beside the amount: a receipt photo from the camera or the gallery, then read.</summary>
        [RelayCommand]
        private async Task ScanReceiptAsync()
        {
            var choice = await _dialogService.ActionSheetAsync(AppResources.ScanReceipt, AppResources.Cancel,
                [AppResources.TakePhoto, AppResources.ChoosePhoto]);
            if (choice == AppResources.TakePhoto)
                await TakePhotoAsync();
            else if (choice == AppResources.ChoosePhoto)
                await PickPhotoAsync();
        }

        /// <summary>Reads a new receipt photo and fills the form as <see cref="ReceiptFill"/> says, with a toast.</summary>
        private async Task ReadReceiptAsync(string path)
        {
            if (!_receiptScanner.IsSupported)
                return;

            ReceiptReading reading;
            IsReadingReceipt = true;
            try
            {
                var lines = await _receiptScanner.ReadLinesAsync(path);
                reading = ReceiptReader.Read(lines, DateTime.Today);
            }
            catch (Exception ex)
            {
                // The photo stays attached; the form is filled by hand as before
                _logger.LogWarning(ex, "Reading the receipt failed");
                reading = ReceiptReading.None;
            }
            finally
            {
                IsReadingReceipt = false;
            }

            // Another photo (or none) since: this reading no longer applies
            if (_receipt.Path != path)
                return;

            var currency = SelectedAccount?.CurrencyCode;
            long? typedCents = string.IsNullOrWhiteSpace(Amount) ? null : Money.ToCents(AmountParser.Parse(Amount, currency));
            var fill = ReceiptFill.Apply(reading, typedCents, !IsEditMode, SelectedDate);

            var filled = new List<string>();
            if (fill.Amount is { } amount)
            {
                Amount = amount.ToString();
                filled.Add(MoneyFormatter.Format(amount, currency));
            }
            if (fill.Date is { } date)
            {
                SelectedDate = date;
                filled.Add(DateFormatter.Format(date, DateStyle.Medium));
            }

            if (fill.Differs is { } total)
                await _dialogService.ToastAsync(string.Format(AppResources.ReceiptSays, MoneyFormatter.Format(total, currency)));
            else if (filled.Count > 0)
                await _dialogService.ToastAsync(string.Format(AppResources.ReceiptFilledIn, string.Join(" · ", filled)));
            else if (reading.IsEmpty)
                await _dialogService.ToastAsync(AppResources.ReceiptNotRead);
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

