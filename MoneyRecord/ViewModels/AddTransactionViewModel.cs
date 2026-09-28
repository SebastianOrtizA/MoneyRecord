using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Behaviors;
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

        [ObservableProperty]
        private List<Category> categories = new();

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

        [ObservableProperty]
        private bool isMonthlyFrequency;

        partial void OnSelectedFrequencyItemChanged(RecurrenceFrequencyItem? value)
        {
            IsMonthlyFrequency = value?.Frequency == RecurrenceFrequency.Monthly;
            if (!IsMonthlyFrequency)
                UseSpecificDay = false;
        }

        public AddTransactionViewModel(IAccountRepository accountRepository, ICategoryRepository categoryRepository, IBalanceService balanceService, ITransactionRepository transactionRepository, IBudgetRepository budgetRepository, IRecurringTransactionRepository recurringTransactionRepository, INavigationService navigationService, IErrorHandler errorHandler)
        {
            _accountRepository = accountRepository;
            _categoryRepository = categoryRepository;
            _balanceService = balanceService;
            _transactionRepository = transactionRepository;
            _budgetRepository = budgetRepository;
            _recurringTransactionRepository = recurringTransactionRepository;
            _navigationService = navigationService;
            _errorHandler = errorHandler;
            selectedFrequencyItem = Frequencies[3];
        }

        /// <summary>
        /// Resets the ViewModel state for a fresh start
        /// </summary>
        private void ResetState()
        {
            Transaction = null;
            TransactionType = TransactionType.Expense;
            SelectedDate = DateTime.Now;
            Description = string.Empty;
            Amount = string.Empty;
            SelectedCategory = null;
            SelectedAccount = null;
            Title = AppResources.AddTransaction;
            IsEditMode = false;
            TagsText = string.Empty;
            ReceiptPhotoPath = null;
            HasReceipt = false;
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

            await LoadAccountsAsync();
            await LoadCategoriesAsync();
            
            if (Transaction != null)
            {
                // Edit mode
                IsEditMode = true;
                TransactionType = Transaction.Type;
                SelectedDate = Transaction.Date;
                Description = Transaction.Description;
                Amount = Transaction.Amount.ToString();
                TagsText = Transaction.Tags ?? string.Empty;
                ReceiptPhotoPath = Transaction.ReceiptPhotoPath;
                HasReceipt = !string.IsNullOrWhiteSpace(ReceiptPhotoPath);

                Title = TransactionType == TransactionType.Income ? AppResources.EditIncome : AppResources.EditExpense;
                
                // Load categories and select the current one
                await LoadCategoriesAsync();
                SelectedCategory = Categories.FirstOrDefault(c => c.Id == Transaction.CategoryId);
                
                // Select the current account
                if (Transaction.AccountId.HasValue)
                {
                    SelectedAccount = Accounts.FirstOrDefault(a => a.Id == Transaction.AccountId.Value);
                }
                else
                {
                    SelectedAccount = Accounts.FirstOrDefault(a => a.IsDefault);
                }
            }
            else
            {
                // Add mode
                IsEditMode = false;
                Title = TransactionType == TransactionType.Income ? AppResources.AddIncome : AppResources.AddExpense;
                
                // Default to Cash account
                SelectedAccount = Accounts.FirstOrDefault(a => a.IsDefault) ?? Accounts.FirstOrDefault();
            }
        }

        private async Task LoadAccountsAsync()
        {
            Accounts = await _accountRepository.GetAllAsync();
        }

        private async Task LoadCategoriesAsync()
        {
            var categoryType = TransactionType == TransactionType.Income ? CategoryType.Income : CategoryType.Expense;
            Categories = await _categoryRepository.GetByTypeAsync(categoryType);

            if (!IsEditMode)
            {
                SelectedCategory = Categories.FirstOrDefault();
            }
        }

        partial void OnSelectedCategoryChanged(Category? value)
        {
            _ = CheckBudgetAsync();
        }

        private async Task CheckBudgetAsync()
        {
            if (TransactionType != TransactionType.Expense || SelectedCategory == null)
            {
                ShowBudgetWarning = false;
                return;
            }

            try
            {
                var budget = await _budgetRepository.GetByCategoryIdAsync(SelectedCategory.Id);
                if (budget == null)
                {
                    ShowBudgetWarning = false;
                    return;
                }

                var now = DateTime.Now;
                var (startDate, endDate) = budget.Period switch
                {
                    BudgetPeriod.Day => (now.Date, now.Date.AddDays(1).AddTicks(-1)),
                    BudgetPeriod.Month => (new DateTime(now.Year, now.Month, 1), new DateTime(now.Year, now.Month, 1).AddMonths(1).AddTicks(-1)),
                    BudgetPeriod.Year => (new DateTime(now.Year, 1, 1), new DateTime(now.Year, 12, 31, 23, 59, 59)),
                    _ => (new DateTime(now.Year, now.Month, 1), new DateTime(now.Year, now.Month, 1).AddMonths(1).AddTicks(-1))
                };

                var spent = await _balanceService.GetCategoryExpensesAsync(SelectedCategory.Id, startDate, endDate);
                var remaining = budget.LimitAmount - spent;

                if (remaining <= 0)
                {
                    BudgetWarningText = string.Format(AppResources.BudgetExceeded, SelectedCategory.Name, spent, budget.LimitAmount);
                    ShowBudgetWarning = true;
                }
                else if (remaining < budget.LimitAmount * 0.2m)
                {
                    BudgetWarningText = string.Format(AppResources.BudgetNearLimit, SelectedCategory.Name, remaining);
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
            if (string.IsNullOrWhiteSpace(Description))
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterDescription);
                return;
            }

            var amountValue = CurrencyMaskBehavior.ParseCurrencyValue(Amount);
            if (amountValue <= 0)
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
                Transaction.Description = Description;
                Transaction.Amount = amountValue;
                Transaction.CategoryId = SelectedCategory.Id;
                Transaction.Type = TransactionType;
                Transaction.AccountId = accountId;
                Transaction.Tags = string.IsNullOrWhiteSpace(TagsText) ? null : TagsText.Trim();
                Transaction.ReceiptPhotoPath = ReceiptPhotoPath;

                await _transactionRepository.SaveAsync(Transaction);
                await Toast.Make(AppResources.TransactionUpdatedSuccessfully).Show();
            }
            else
            {
                // Create new transaction
                var newTransaction = new Transaction
                {
                    Date = SelectedDate,
                    Description = Description,
                    Amount = amountValue,
                    CategoryId = SelectedCategory.Id,
                    Type = TransactionType,
                    AccountId = accountId,
                    Tags = string.IsNullOrWhiteSpace(TagsText) ? null : TagsText.Trim(),
                    ReceiptPhotoPath = ReceiptPhotoPath
                };

                await _transactionRepository.SaveAsync(newTransaction);

                if (IsRecurring && SelectedFrequencyItem != null)
                {
                    var frequency = SelectedFrequencyItem.Frequency;
                    int? dayOfMonth = (frequency == RecurrenceFrequency.Monthly && UseSpecificDay)
                        ? SelectedDayOfMonth
                        : null;

                    DateTime nextOccurrence;
                    if (dayOfMonth.HasValue)
                    {
                        var next = SelectedDate.AddMonths(1);
                        var day = Math.Min(dayOfMonth.Value, DateTime.DaysInMonth(next.Year, next.Month));
                        nextOccurrence = new DateTime(next.Year, next.Month, day);
                    }
                    else
                    {
                        nextOccurrence = frequency switch
                        {
                            RecurrenceFrequency.Daily => SelectedDate.AddDays(1),
                            RecurrenceFrequency.Weekly => SelectedDate.AddDays(7),
                            RecurrenceFrequency.Biweekly => SelectedDate.AddDays(14),
                            RecurrenceFrequency.Monthly => SelectedDate.AddMonths(1),
                            RecurrenceFrequency.Yearly => SelectedDate.AddYears(1),
                            _ => SelectedDate.AddMonths(1)
                        };
                    }

                    var recurring = new RecurringTransaction
                    {
                        Amount = amountValue,
                        Description = Description,
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
                }
            }

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
            var receiptsDir = Path.Combine(FileSystem.AppDataDirectory, "receipts");
            Directory.CreateDirectory(receiptsDir);

            var fileName = $"receipt_{DateTime.Now:yyyyMMdd_HHmmss}{Path.GetExtension(photo.FileName)}";
            var filePath = Path.Combine(receiptsDir, fileName);

            using var stream = await photo.OpenReadAsync();
            using var fileStream = File.OpenWrite(filePath);
            await stream.CopyToAsync(fileStream);

            ReceiptPhotoPath = filePath;
            HasReceipt = true;
        }

        [RelayCommand]
        private void RemovePhoto()
        {
            ReceiptPhotoPath = null;
            HasReceipt = false;
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            await _navigationService.GoBackAsync();
        }
    }
}

