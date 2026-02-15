using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using System.Collections.ObjectModel;

namespace MoneyRecord.ViewModels
{
    public partial class BudgetViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        [ObservableProperty]
        private PeriodItem selectedPeriod;

        [ObservableProperty]
        private DateTime customStartDate = DateTime.Now.AddMonths(-1);

        [ObservableProperty]
        private DateTime customEndDate = DateTime.Now;

        [ObservableProperty]
        private bool isCustomPeriodSelected = false;

        [ObservableProperty]
        private bool isLoading = false;

        [ObservableProperty]
        private bool hasBudgets = false;

        [ObservableProperty]
        private ObservableCollection<BudgetProgress> budgets = new();

        [ObservableProperty]
        private bool isAddFormVisible = false;

        [ObservableProperty]
        private List<Category> availableCategories = new();

        [ObservableProperty]
        private Category? selectedCategory;

        [ObservableProperty]
        private string budgetAmount = string.Empty;

        [ObservableProperty]
        private decimal totalBudgeted;

        [ObservableProperty]
        private decimal totalSpent;

        [ObservableProperty]
        private decimal totalRemaining;

        public List<PeriodItem> Periods { get; } = PeriodHelper.GetReportPeriods();

        public BudgetViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            selectedPeriod = PeriodHelper.GetDefaultPeriod();
        }

        public async Task InitializeAsync()
        {
            await LoadAvailableCategoriesAsync();
            await LoadBudgetsAsync();
        }

        private async Task LoadAvailableCategoriesAsync()
        {
            var expenseCategories = await _databaseService.GetCategoriesAsync(CategoryType.Expense);
            var existingBudgetCategoryIds = (await _databaseService.GetBudgetsAsync())
                .Where(b => b.IsActive)
                .Select(b => b.CategoryId)
                .ToHashSet();

            // Filter out categories that already have a budget
            AvailableCategories = expenseCategories
                .Where(c => !existingBudgetCategoryIds.Contains(c.Id))
                .ToList();
        }

        [RelayCommand]
        private async Task LoadBudgetsAsync()
        {
            try
            {
                IsLoading = true;

                var (startDate, endDate) = GetDateRange();

                var allBudgets = await _databaseService.GetBudgetsAsync();
                var activeBudgets = allBudgets.Where(b => b.IsActive).ToList();

                var budgetProgressList = new List<BudgetProgress>();

                foreach (var budget in activeBudgets)
                {
                    var category = await _databaseService.GetCategoryByIdAsync(budget.CategoryId);
                    if (category == null) continue;

                    var spentAmount = await _databaseService.GetCategoryExpensesAsync(budget.CategoryId, startDate, endDate);

                    var progress = new BudgetProgress
                    {
                        BudgetId = budget.Id,
                        CategoryId = budget.CategoryId,
                        CategoryName = category.Name,
                        CategoryIconCode = category.IconCode,
                        LimitAmount = budget.LimitAmount,
                        SpentAmount = spentAmount
                    };
                    progress.CalculateProgress();

                    budgetProgressList.Add(progress);
                }

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Budgets.Clear();
                    foreach (var progress in budgetProgressList.OrderByDescending(b => b.ProgressPercentage))
                    {
                        Budgets.Add(progress);
                    }
                    HasBudgets = Budgets.Any();

                    // Calculate totals
                    TotalBudgeted = budgetProgressList.Sum(b => b.LimitAmount);
                    TotalSpent = budgetProgressList.Sum(b => b.SpentAmount);
                    TotalRemaining = TotalBudgeted - TotalSpent;
                });

                // Refresh available categories
                await LoadAvailableCategoriesAsync();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync(AppResources.Error, 
                    string.Format(AppResources.FailedToLoadReportData, ex.Message), 
                    AppResources.OK);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void ShowAddForm()
        {
            IsAddFormVisible = true;
            SelectedCategory = AvailableCategories.FirstOrDefault();
            BudgetAmount = string.Empty;
        }

        [RelayCommand]
        private void CancelAddForm()
        {
            IsAddFormVisible = false;
            SelectedCategory = null;
            BudgetAmount = string.Empty;
        }

        [RelayCommand]
        private async Task AddBudgetAsync()
        {
            if (SelectedCategory == null)
            {
                await Shell.Current.DisplayAlertAsync(AppResources.Error, 
                    AppResources.PleaseSelectCategory, 
                    AppResources.OK);
                return;
            }

            if (!decimal.TryParse(BudgetAmount.Replace("$", "").Replace(",", ""), out var amount) || amount <= 0)
            {
                await Shell.Current.DisplayAlertAsync(AppResources.Error, 
                    AppResources.PleaseEnterValidAmount, 
                    AppResources.OK);
                return;
            }

            try
            {
                var budget = new Budget
                {
                    CategoryId = SelectedCategory.Id,
                    LimitAmount = amount,
                    CreatedDate = DateTime.Now,
                    IsActive = true
                };

                await _databaseService.SaveBudgetAsync(budget);

                IsAddFormVisible = false;
                SelectedCategory = null;
                BudgetAmount = string.Empty;

                await LoadBudgetsAsync();

                await Shell.Current.DisplayAlertAsync(AppResources.Success, 
                    AppResources.BudgetAddedSuccessfully, 
                    AppResources.OK);
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync(AppResources.Error, 
                    string.Format(AppResources.FailedToSaveBudget, ex.Message), 
                    AppResources.OK);
            }
        }

        [RelayCommand]
        private async Task DeleteBudgetAsync(BudgetProgress budget)
        {
            if (budget == null) return;

            var confirm = await Shell.Current.DisplayAlertAsync(
                AppResources.ConfirmDelete,
                string.Format(AppResources.DeleteBudgetConfirmation, budget.CategoryName),
                AppResources.YesDelete,
                AppResources.Cancel);

            if (!confirm) return;

            try
            {
                await _databaseService.DeleteBudgetAsync(budget.BudgetId);
                await LoadBudgetsAsync();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync(AppResources.Error, 
                    string.Format(AppResources.FailedToDeleteBudget, ex.Message), 
                    AppResources.OK);
            }
        }

        [RelayCommand]
        private async Task EditBudgetAsync(BudgetProgress budget)
        {
            if (budget == null) return;

            // Simple edit using prompt - could be enhanced with a proper edit form
            var result = await Shell.Current.DisplayPromptAsync(
                AppResources.EditBudget,
                string.Format(AppResources.EnterNewBudgetLimit, budget.CategoryName),
                AppResources.Save,
                AppResources.Cancel,
                initialValue: budget.LimitAmount.ToString("N2"),
                keyboard: Keyboard.Numeric);

            if (string.IsNullOrEmpty(result)) return;

            if (!decimal.TryParse(result.Replace("$", "").Replace(",", ""), out var newAmount) || newAmount <= 0)
            {
                await Shell.Current.DisplayAlertAsync(AppResources.Error, 
                    AppResources.PleaseEnterValidAmount, 
                    AppResources.OK);
                return;
            }

            try
            {
                await _databaseService.UpdateBudgetAmountAsync(budget.BudgetId, newAmount);
                await LoadBudgetsAsync();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync(AppResources.Error, 
                    string.Format(AppResources.FailedToSaveBudget, ex.Message), 
                    AppResources.OK);
            }
        }

        partial void OnSelectedPeriodChanged(PeriodItem value)
        {
            IsCustomPeriodSelected = value?.Type == PeriodType.CustomPeriod;
            _ = LoadBudgetsAsync();
        }

        partial void OnCustomStartDateChanged(DateTime value)
        {
            if (IsCustomPeriodSelected)
            {
                _ = LoadBudgetsAsync();
            }
        }

        partial void OnCustomEndDateChanged(DateTime value)
        {
            if (IsCustomPeriodSelected)
            {
                _ = LoadBudgetsAsync();
            }
        }

        private (DateTime startDate, DateTime endDate) GetDateRange()
        {
            return DateRangeHelper.GetDateRange(SelectedPeriod?.Type, CustomStartDate, CustomEndDate);
        }
    }
}
