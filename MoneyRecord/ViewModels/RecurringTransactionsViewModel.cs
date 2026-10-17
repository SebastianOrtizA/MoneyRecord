using MoneyRecord.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Views;
using System.Collections.ObjectModel;

namespace MoneyRecord.ViewModels
{
    [QueryProperty(nameof(Show), "Show")]
    public partial class RecurringTransactionsViewModel : ObservableObject
    {
        /// <summary>The <c>Show</c> query value that opens the page on the Upcoming list (Home's "See all").</summary>
        public const string ShowUpcoming = "Upcoming";

        private readonly IRecurringTransactionRepository _recurringRepo;
        private readonly ICategoryRepository _categoryRepo;
        private readonly IAccountRepository _accountRepo;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;
        private readonly IDebtService _debtService;
        private readonly INavigationService _navigationService;

        [ObservableProperty]
        private ObservableCollection<RecurringTransaction> recurringTransactions = [];

        [ObservableProperty]
        private bool hasRecurringTransactions;

        /// <summary>The page shows the next 30 days of occurrences instead of the schedules.</summary>
        [ObservableProperty]
        private bool isUpcomingView;

        [ObservableProperty]
        private List<UpcomingEntry> upcomingEntries = [];

        [ObservableProperty]
        private bool hasUpcoming;

        public string? Show
        {
            set => IsUpcomingView = value == ShowUpcoming;
        }

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool isEditMode;

        [ObservableProperty]
        private RecurringTransaction? editingItem;

        [ObservableProperty]
        private RecurrenceFrequencyItem? editSelectedFrequency;

        [ObservableProperty]
        private bool editUseSpecificDay;

        [ObservableProperty]
        private int editSelectedDayOfMonth = 1;

        [ObservableProperty]
        private bool editIsMonthlyFrequency;

        [ObservableProperty]
        private string editAmount = string.Empty;

        /// <summary>The schedule has created nothing yet: its first date can still move (tomorrow at the earliest).</summary>
        [ObservableProperty]
        private bool editCanMoveFirstDate;

        [ObservableProperty]
        private DateTime editFirstDate;

        public DateTime EditMinimumDate => RecurringStart.EarliestScheduleDate(DateTime.Now);

        public List<RecurrenceFrequencyItem> Frequencies { get; } =
        [
            new() { Frequency = RecurrenceFrequency.Daily, Display = AppResources.FrequencyDaily },
            new() { Frequency = RecurrenceFrequency.Weekly, Display = AppResources.FrequencyWeekly },
            new() { Frequency = RecurrenceFrequency.Biweekly, Display = AppResources.FrequencyBiweekly },
            new() { Frequency = RecurrenceFrequency.Monthly, Display = AppResources.FrequencyMonthly },
            new() { Frequency = RecurrenceFrequency.Yearly, Display = AppResources.FrequencyYearly },
        ];

        public List<int> DaysOfMonth { get; } = Enumerable.Range(1, 31).ToList();

        public RecurringTransactionsViewModel(
            IRecurringTransactionRepository recurringRepo,
            ICategoryRepository categoryRepo,
            IAccountRepository accountRepo,
            IErrorHandler errorHandler, IDialogService dialogService, IDebtService debtService,
            INavigationService navigationService)
        {
            _debtService = debtService;
            _navigationService = navigationService;
            _recurringRepo = recurringRepo;
            _categoryRepo = categoryRepo;
            _accountRepo = accountRepo;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
        }

        public async Task InitializeAsync()
        {
            await LoadRecurringTransactionsAsync();
        }

        partial void OnEditSelectedFrequencyChanged(RecurrenceFrequencyItem? value)
        {
            EditIsMonthlyFrequency = value?.Frequency == RecurrenceFrequency.Monthly;
            if (!EditIsMonthlyFrequency)
                EditUseSpecificDay = false;
        }

        [RelayCommand]
        private async Task LoadRecurringTransactionsAsync()
        {
            try
            {
                IsLoading = true;

                var items = await _recurringRepo.GetActiveAsync();
                var categories = (await _categoryRepo.GetAllAsync()).ToDictionary(c => c.Id);
                var accounts = (await _accountRepo.GetAllAsync()).ToDictionary(a => a.Id);

                foreach (var item in items)
                {
                    // A debt's payment schedule: its category is only the interest's
                    if (item.DebtId != null)
                    {
                        item.CategoryName = AppResources.Debts;
                        item.CategoryIconCode = DebtSummary.IconCode;
                    }
                    else if (categories.TryGetValue(item.CategoryId, out var category))
                    {
                        item.CategoryName = category.Name;
                        item.CategoryIconCode = category.IconCode;
                    }

                    if (accounts.TryGetValue(item.AccountId, out var account))
                    {
                        item.AccountName = account.Name;
                        item.CurrencyCode = account.CurrencyCode;
                    }

                    item.FrequencyDisplay = GetFrequencyDisplay(item.Frequency, item.DayOfMonth);
                }

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    RecurringTransactions.Clear();
                    foreach (var item in items.OrderBy(r => r.NextOccurrence))
                    {
                        RecurringTransactions.Add(item);
                    }
                    HasRecurringTransactions = RecurringTransactions.Count > 0;
                });

                // From the same rows, after any change to the schedules (edit, delete, pause), so both lists agree
                var debtsLeft = items.Any(r => r.DebtId != null)
                    ? await _debtService.GetOutstandingAsync()
                    : null;
                UpcomingEntries = UpcomingSchedule.Build(items, categories, accounts, DateTime.Now, debtsLeft: debtsLeft);
                HasUpcoming = UpcomingEntries.Count > 0;
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadRecurringTransactions);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void SelectSchedules() => IsUpcomingView = false;

        [RelayCommand]
        private void SelectUpcoming()
        {
            // The edit form belongs to the schedules list
            CancelEdit();
            IsUpcomingView = true;
        }

        [RelayCommand]
        private void EditRecurring(RecurringTransaction item)
        {
            if (item == null) return;

            EditingItem = item;
            EditSelectedFrequency = Frequencies.FirstOrDefault(f => f.Frequency == item.Frequency) ?? Frequencies[3];
            EditUseSpecificDay = item.DayOfMonth.HasValue;
            EditSelectedDayOfMonth = item.DayOfMonth ?? 1;
            EditAmount = item.Amount.ToString("N2");
            // A debt's schedule is edited on the Debts page
            EditCanMoveFirstDate = item.DebtId == null && item.LastProcessedDate == null
                && RecurringStart.IsFuture(item.NextOccurrence, DateTime.Now);
            EditFirstDate = item.NextOccurrence;
            IsEditMode = true;
        }

        /// <summary>The + button: a new recurring expense or income, from tomorrow on (the add form in its schedule mode).</summary>
        [RelayCommand]
        private async Task AddRecurringAsync()
        {
            CancelEdit();
            var choice = await _dialogService.ActionSheetAsync(AppResources.AddRecurring, AppResources.Cancel,
                [AppResources.Expense, AppResources.Income]);
            var type = choice == AppResources.Expense ? TransactionType.Expense
                : choice == AppResources.Income ? TransactionType.Income
                : (TransactionType?)null;
            if (type == null)
                return;

            await _navigationService.NavigateToAsync(nameof(AddTransactionPage),
                new Dictionary<string, object> { { "TransactionType", type.Value }, { "Schedule", bool.TrueString } });
        }

        [RelayCommand]
        private void CancelEdit()
        {
            IsEditMode = false;
            EditingItem = null;
        }

        [RelayCommand]
        private async Task SaveEditAsync()
        {
            if (EditingItem == null || EditSelectedFrequency == null) return;

            var newAmount = AmountParser.Parse(EditAmount, EditingItem.CurrencyCode);
            if (newAmount <= 0 || !Money.IsInRange(newAmount))
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterValidAmount);
                return;
            }

            if (EditCanMoveFirstDate && !RecurringStart.IsFuture(EditFirstDate, DateTime.Now))
            {
                await _errorHandler.HandleAsync(AppResources.ScheduleDateMustBeFuture);
                return;
            }

            try
            {
                var frequency = EditSelectedFrequency.Frequency;
                int? dayOfMonth = (frequency == RecurrenceFrequency.Monthly && EditUseSpecificDay)
                    ? EditSelectedDayOfMonth
                    : null;

                EditingItem.Frequency = frequency;
                EditingItem.DayOfMonth = dayOfMonth;
                EditingItem.Amount = newAmount;
                if (EditCanMoveFirstDate)
                {
                    // Nothing created yet: the schedule starts on the new date (a monthly one keeps that day)
                    EditingItem.StartDate = EditFirstDate.Date;
                    EditingItem.NextOccurrence = EditFirstDate.Date;
                }

                // Recalculate next occurrence from now
                var now = DateTime.Now;
                if (EditingItem.NextOccurrence <= now)
                {
                    EditingItem.NextOccurrence = RecurrenceCalculator.NextAfter(now, frequency, EditingItem.EffectiveDayOfMonth);
                }

                await _recurringRepo.SaveAsync(EditingItem);

                IsEditMode = false;
                EditingItem = null;

                await LoadRecurringTransactionsAsync();
                await _dialogService.ToastAsync(AppResources.RecurringTransactionUpdated);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToUpdateRecurringTransaction);
            }
        }

        [RelayCommand]
        private async Task DeleteRecurringAsync(RecurringTransaction item)
        {
            if (item == null) return;

            var confirm = await _dialogService.ConfirmAsync(
                AppResources.ConfirmDelete,
                string.Format(AppResources.DeleteRecurringConfirmation, item.Description),
                AppResources.YesDelete,
                AppResources.Cancel);

            if (!confirm) return;

            try
            {
                await _recurringRepo.DeleteByIdAsync(item.Id);
                await LoadRecurringTransactionsAsync();
                await _dialogService.ToastAsync(AppResources.RecurringTransactionDeleted);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToDeleteRecurringTransaction);
            }
        }

        [RelayCommand]
        private async Task ToggleActiveAsync(RecurringTransaction item)
        {
            if (item == null) return;

            try
            {
                if (item.IsActive)
                {
                    await _recurringRepo.DeactivateAsync(item.Id);
                }
                else
                {
                    item.IsActive = true;
                    await _recurringRepo.SaveAsync(item);
                }

                await LoadRecurringTransactionsAsync();
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToUpdateRecurringTransaction);
            }
        }

        private static string GetFrequencyDisplay(RecurrenceFrequency frequency, int? dayOfMonth)
        {
            var label = frequency switch
            {
                RecurrenceFrequency.Daily => AppResources.FrequencyDaily,
                RecurrenceFrequency.Weekly => AppResources.FrequencyWeekly,
                RecurrenceFrequency.Biweekly => AppResources.FrequencyBiweekly,
                RecurrenceFrequency.Monthly => AppResources.FrequencyMonthly,
                RecurrenceFrequency.Yearly => AppResources.FrequencyYearly,
                _ => AppResources.FrequencyMonthly
            };

            if (frequency == RecurrenceFrequency.Monthly && dayOfMonth.HasValue)
            {
                label += $" ({string.Format(AppResources.DayN, dayOfMonth.Value)})";
            }

            return label;
        }
    }
}
