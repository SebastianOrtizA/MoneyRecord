using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using System.Collections.ObjectModel;

namespace MoneyRecord.ViewModels
{
    public partial class BudgetViewModel : ObservableObject
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IBudgetRepository _budgetRepository;
        private readonly IBalanceService _balanceService;
        private readonly IErrorHandler _errorHandler;

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
        private BudgetPeriodItem selectedBudgetPeriod;

        [ObservableProperty]
        private decimal totalBudgeted;

        [ObservableProperty]
        private decimal totalSpent;

        [ObservableProperty]
        private decimal totalRemaining;

        public List<PeriodItem> Periods { get; } = PeriodHelper.GetReportPeriods();

        public List<BudgetPeriodItem> BudgetPeriods { get; } =
        [
            new BudgetPeriodItem { Period = BudgetPeriod.Day },
            new BudgetPeriodItem { Period = BudgetPeriod.Month },
            new BudgetPeriodItem { Period = BudgetPeriod.Year }
        ];

        public BudgetViewModel(ICategoryRepository categoryRepository, IBudgetRepository budgetRepository, IBalanceService balanceService, IErrorHandler errorHandler)
        {
            _categoryRepository = categoryRepository;
            _budgetRepository = budgetRepository;
            _balanceService = balanceService;
            _errorHandler = errorHandler;
            selectedPeriod = PeriodHelper.GetDefaultPeriod();
            selectedBudgetPeriod = BudgetPeriods[1]; // Default to Month
        }

        public async Task InitializeAsync()
        {
            await LoadAvailableCategoriesAsync();
            await LoadBudgetsAsync();
        }

        private async Task LoadAvailableCategoriesAsync()
        {
            var expenseCategories = await _categoryRepository.GetByTypeAsync(CategoryType.Expense);
            var existingBudgetCategoryIds = (await _budgetRepository.GetActiveBudgetsAsync())
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

                var activeBudgets = await _budgetRepository.GetActiveBudgetsAsync();

                var budgetProgressList = new List<BudgetProgress>();

                foreach (var budget in activeBudgets)
                {
                    var category = await _categoryRepository.GetByIdAsync(budget.CategoryId);
                    if (category == null) continue;

                    var spentAmount = await _balanceService.GetCategoryExpensesAsync(budget.CategoryId, startDate, endDate);

                    // Calculate projected limit based on budget period and selected date range
                    var projectedLimit = BudgetProjectionHelper.CalculateProjectedLimit(
                        budget.LimitAmount,
                        budget.Period,
                        SelectedPeriod?.Type ?? PeriodType.CalendarMonth,
                        CustomStartDate,
                        CustomEndDate);

                    var progress = new BudgetProgress
                    {
                        BudgetId = budget.Id,
                        CategoryId = budget.CategoryId,
                        CategoryName = category.Name,
                        CategoryIconCode = category.IconCode,
                        Period = budget.Period,
                        OriginalLimitAmount = budget.LimitAmount,
                        LimitAmount = projectedLimit,
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
                await _errorHandler.HandleAsync(ex, string.Format(AppResources.FailedToLoadReportData, ex.Message));
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
            SelectedBudgetPeriod = BudgetPeriods[1]; // Default to Month
            BudgetAmount = string.Empty;
        }

        [RelayCommand]
        private void CancelAddForm()
        {
            IsAddFormVisible = false;
            SelectedCategory = null;
            SelectedBudgetPeriod = BudgetPeriods[1]; // Reset to Month
            BudgetAmount = string.Empty;
        }

        [RelayCommand]
        private async Task AddBudgetAsync()
        {
            if (SelectedCategory == null)
            {
                await _errorHandler.HandleAsync(AppResources.PleaseSelectCategory);
                return;
            }

            if (!decimal.TryParse(BudgetAmount.Replace("$", "").Replace(",", ""), out var amount) || amount <= 0)
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterValidAmount);
                return;
            }

            try
            {
                var budget = new Budget
                {
                    CategoryId = SelectedCategory.Id,
                    LimitAmount = amount,
                    Period = SelectedBudgetPeriod?.Period ?? BudgetPeriod.Month,
                    CreatedDate = DateTime.Now,
                    IsActive = true
                };

                await _budgetRepository.SaveAsync(budget);

                IsAddFormVisible = false;
                SelectedCategory = null;
                SelectedBudgetPeriod = BudgetPeriods[1]; // Reset to Month
                BudgetAmount = string.Empty;

                await LoadBudgetsAsync();

                await Toast.Make(AppResources.BudgetAddedSuccessfully).Show();
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, string.Format(AppResources.FailedToSaveBudget, ex.Message));
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
                await _budgetRepository.DeleteByIdAsync(budget.BudgetId);
                await LoadBudgetsAsync();
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, string.Format(AppResources.FailedToDeleteBudget, ex.Message));
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
                await _errorHandler.HandleAsync(AppResources.PleaseEnterValidAmount);
                return;
            }

            try
            {
                await _budgetRepository.UpdateAmountAsync(budget.BudgetId, newAmount);
                await LoadBudgetsAsync();
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, string.Format(AppResources.FailedToSaveBudget, ex.Message));
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
