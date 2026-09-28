using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using System.Collections.ObjectModel;

namespace MoneyRecord.ViewModels
{
    public partial class RecurringTransactionsViewModel : ObservableObject
    {
        private readonly IRecurringTransactionRepository _recurringRepo;
        private readonly ICategoryRepository _categoryRepo;
        private readonly IAccountRepository _accountRepo;
        private readonly IErrorHandler _errorHandler;

        [ObservableProperty]
        private ObservableCollection<RecurringTransaction> recurringTransactions = [];

        [ObservableProperty]
        private bool hasRecurringTransactions;

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
            IErrorHandler errorHandler)
        {
            _recurringRepo = recurringRepo;
            _categoryRepo = categoryRepo;
            _accountRepo = accountRepo;
            _errorHandler = errorHandler;
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
                    if (categories.TryGetValue(item.CategoryId, out var category))
                    {
                        item.CategoryName = category.Name;
                        item.CategoryIconCode = category.IconCode;
                    }

                    if (accounts.TryGetValue(item.AccountId, out var account))
                    {
                        item.AccountName = account.Name;
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
        private void EditRecurring(RecurringTransaction item)
        {
            if (item == null) return;

            EditingItem = item;
            EditSelectedFrequency = Frequencies.FirstOrDefault(f => f.Frequency == item.Frequency) ?? Frequencies[3];
            EditUseSpecificDay = item.DayOfMonth.HasValue;
            EditSelectedDayOfMonth = item.DayOfMonth ?? 1;
            EditAmount = item.Amount.ToString("N2");
            IsEditMode = true;
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

            if (!decimal.TryParse(EditAmount.Replace("$", "").Replace(",", ""), out var newAmount) || newAmount <= 0)
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterValidAmount);
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

                // Recalculate next occurrence from now
                var now = DateTime.Now;
                if (EditingItem.NextOccurrence <= now)
                {
                    EditingItem.NextOccurrence = CalculateNextFromNow(now, frequency, dayOfMonth);
                }

                await _recurringRepo.SaveAsync(EditingItem);

                IsEditMode = false;
                EditingItem = null;

                await LoadRecurringTransactionsAsync();
                await Toast.Make(AppResources.RecurringTransactionUpdated).Show();
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToUpdateRecurringTransaction);
            }
        }

        private static DateTime CalculateNextFromNow(DateTime now, RecurrenceFrequency frequency, int? dayOfMonth)
        {
            if (frequency == RecurrenceFrequency.Monthly && dayOfMonth.HasValue)
            {
                var day = Math.Min(dayOfMonth.Value, DateTime.DaysInMonth(now.Year, now.Month));
                var candidate = new DateTime(now.Year, now.Month, day);
                return candidate > now ? candidate : candidate.AddMonths(1);
            }

            return frequency switch
            {
                RecurrenceFrequency.Daily => now.Date.AddDays(1),
                RecurrenceFrequency.Weekly => now.Date.AddDays(7),
                RecurrenceFrequency.Biweekly => now.Date.AddDays(14),
                RecurrenceFrequency.Monthly => now.Date.AddMonths(1),
                RecurrenceFrequency.Yearly => now.Date.AddYears(1),
                _ => now.Date.AddMonths(1)
            };
        }

        [RelayCommand]
        private async Task DeleteRecurringAsync(RecurringTransaction item)
        {
            if (item == null) return;

            var confirm = await Shell.Current.DisplayAlertAsync(
                AppResources.ConfirmDelete,
                string.Format(AppResources.DeleteRecurringConfirmation, item.Description),
                AppResources.YesDelete,
                AppResources.Cancel);

            if (!confirm) return;

            try
            {
                await _recurringRepo.DeleteByIdAsync(item.Id);
                await LoadRecurringTransactionsAsync();
                await Toast.Make(AppResources.RecurringTransactionDeleted).Show();
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
