using MoneyRecord.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

        [ObservableProperty]
        private TransactionType transactionType;

        [ObservableProperty]
        private Transaction? transaction;

        [ObservableProperty]
        private DateTime selectedDate = DateTime.Now;

        [ObservableProperty]
        private string description = string.Empty;

        [ObservableProperty]
        private string amount = string.Empty;

        [ObservableProperty]
        private Category? selectedCategory;

        /// <summary>The category grid: recently used first (<see cref="IEntryDefaultsService.OrderByRecent"/>).</summary>
        [ObservableProperty]
        private List<CategoryChoice> categoryChoices = [];

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

        public AddTransactionViewModel(IAccountRepository accountRepository, ICategoryRepository categoryRepository, IBalanceService balanceService, ITransactionRepository transactionRepository, IBudgetRepository budgetRepository, IRecurringTransactionRepository recurringTransactionRepository, INavigationService navigationService, IErrorHandler errorHandler, IDialogService dialogService, IAppPaths paths, IEntryDefaultsService entryDefaults, IReminderService reminders)
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
            SelectedCategory = null;
            SelectedAccount = null;
            Title = AppResources.AddTransaction;
            IsEditMode = false;
            IsMoreDetailsOpen = false;
            TagsText = string.Empty;
            ShowReceipt();
            IsRecurring = false;
            SelectedFrequencyItem = Frequencies[3];
            HasEndDate = false;
            EndDate = DateTime.Now.AddMonths(6);
            UseSpecificDay = false;
            SelectedDayOfMonth = 1;
        }

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
                TransactionType = Transaction.Type;
                await LoadAccountsAsync();
                await LoadCategoriesAsync();
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
                IsMoreDetailsOpen = Description.Length > 0 || Transaction.HasTags || Transaction.HasReceipt;
                
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
            }
            else
            {
                // Add mode
                IsEditMode = false;
                Title = TransactionType == TransactionType.Income ? AppResources.AddIncome : AppResources.AddExpense;
                await LoadAccountsAsync();
                await LoadCategoriesAsync();

                // The account and category last used for this type
                SelectedAccount = _entryDefaults.DefaultAccount(TransactionType, Accounts);
                SelectedCategory = _entryDefaults.DefaultCategory(TransactionType, CategoryChoices.Select(c => c.Category));
            }
        }

        private async Task LoadAccountsAsync()
        {
            Accounts = await _accountRepository.GetAllAsync();
        }

        private async Task LoadCategoriesAsync()
        {
            var categoryType = TransactionType == TransactionType.Income ? CategoryType.Income : CategoryType.Expense;
            var categories = await _categoryRepository.GetByTypeAsync(categoryType);
            CategoryChoices = _entryDefaults.OrderByRecent(TransactionType, categories)
                .Select(c => new CategoryChoice(c))
                .ToList();
        }

        [RelayCommand]
        private void SelectCategory(CategoryChoice choice) => SelectedCategory = choice.Category;

        [RelayCommand]
        private void ToggleMoreDetails() => IsMoreDetailsOpen = !IsMoreDetailsOpen;

        partial void OnSelectedCategoryChanged(Category? value)
        {
            foreach (var choice in CategoryChoices)
                choice.IsSelected = choice.Category.Id == value?.Id;

            _ = CheckBudgetAsync();
        }

        private async Task CheckBudgetAsync()
        {
            var category = SelectedCategory;
            if (TransactionType != TransactionType.Expense || category == null)
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
                    // A budget is in the base currency: only base-currency expenses count until rates exist
                    spent = CurrencyAmount.In(await _balanceService.GetCategoryExpensesAsync(category.Id, startDate, endDate),
                        MoneyFormatter.ResolveCurrencyCode(null));
                }

                // Another category was picked meanwhile: its own check shows its warning
                if (category != SelectedCategory)
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

            if (SelectedCategory == null)
            {
                await _errorHandler.HandleAsync(AppResources.PleaseSelectCategory);
                return;
            }

            int accountId = SelectedAccount.Id;
            var savedDescription = EntryDescription.Resolve(Description, SelectedCategory.Name);

            // Validate balance for expense transactions if account doesn't allow negative balance
            if (TransactionType == TransactionType.Expense && !SelectedAccount.AllowNegativeBalance)
            {
                var currentBalance = await _balanceService.GetAccountBalanceAsync(accountId);

                decimal adjustedBalance = currentBalance;
                if (IsEditMode && Transaction != null && Transaction.Type == TransactionType.Expense)
                {
                    adjustedBalance += Transaction.Amount;
                }

                if (adjustedBalance - amountValue < 0)
                {
                    await _errorHandler.HandleAsync(string.Format(AppResources.InsufficientAccountBalance, SelectedAccount.Name));
                    return;
                }
            }

            if (IsEditMode && Transaction != null)
            {
                // Update existing transaction
                Transaction.Date = SelectedDate;
                Transaction.Description = savedDescription;
                Transaction.Amount = amountValue;
                Transaction.CategoryId = SelectedCategory.Id;
                Transaction.Type = TransactionType;
                Transaction.AccountId = accountId;
                Transaction.Tags = string.IsNullOrWhiteSpace(TagsText) ? null : TagsText.Trim();
                Transaction.ReceiptPhotoPath = ReceiptPhotoPath;

                await _transactionRepository.SaveAsync(Transaction);
                _receipt.Commit(); // deletes a removed or replaced receipt
                await _dialogService.ToastAsync(AppResources.TransactionUpdatedSuccessfully);
            }
            else
            {
                // Create new transaction
                var newTransaction = new Transaction
                {
                    Date = SelectedDate,
                    Description = savedDescription,
                    Amount = amountValue,
                    CategoryId = SelectedCategory.Id,
                    Type = TransactionType,
                    AccountId = accountId,
                    Tags = string.IsNullOrWhiteSpace(TagsText) ? null : TagsText.Trim(),
                    ReceiptPhotoPath = ReceiptPhotoPath
                };

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

            await _navigationService.GoBackAsync();
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

