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
        private readonly IBudgetSummaryService _budgetSummaryService;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;

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

        public BudgetViewModel(ICategoryRepository categoryRepository, IBudgetRepository budgetRepository, IBudgetSummaryService budgetSummaryService, IErrorHandler errorHandler, IDialogService dialogService)
        {
            _categoryRepository = categoryRepository;
            _budgetRepository = budgetRepository;
            _budgetSummaryService = budgetSummaryService;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
            selectedPeriod = PeriodHelper.GetDefaultPeriod();
            selectedBudgetPeriod = BudgetPeriods[1]; // Default to Month
        }

        public Task InitializeAsync() => LoadBudgetsCommand.ExecuteAsync(null);

        private async Task<List<Category>> GetAvailableCategoriesAsync()
        {
            var expenseCategories = await _categoryRepository.GetByTypeAsync(CategoryType.Expense);
            var existingBudgetCategoryIds = (await _budgetRepository.GetActiveBudgetsAsync())
                .Select(b => b.CategoryId)
                .ToHashSet();

            // Filter out categories that already have a budget
            return expenseCategories
                .Where(c => !existingBudgetCategoryIds.Contains(c.Id))
                .ToList();
        }

        /// <summary>Each run cancels the one in progress, so an older period's results never overwrite a newer one's.</summary>
        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadBudgetsAsync(CancellationToken cancellationToken)
        {
            try
            {
                IsLoading = true;

                var summaryTask = _budgetSummaryService.GetSummaryAsync(
                    SelectedPeriod?.Type ?? PeriodType.CalendarMonth, CustomStartDate, CustomEndDate);
                var categoriesTask = GetAvailableCategoriesAsync();
                await Task.WhenAll(summaryTask, categoriesTask);
                cancellationToken.ThrowIfCancellationRequested();

                var budgetProgressList = await summaryTask;
                var availableCategories = await categoriesTask;

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Budgets.Clear();
                    foreach (var progress in budgetProgressList)
                    {
                        Budgets.Add(progress);
                    }
                    HasBudgets = Budgets.Any();

                    // Calculate totals
                    TotalBudgeted = budgetProgressList.Sum(b => b.LimitAmount);
                    TotalSpent = budgetProgressList.Sum(b => b.SpentAmount);
                    TotalRemaining = TotalBudgeted - TotalSpent;
                });

                AvailableCategories = availableCategories;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A newer load replaced this one
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadReportData);
            }
            finally
            {
                // The newer load owns the spinner
                if (!cancellationToken.IsCancellationRequested)
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

            var amount = AmountParser.Parse(BudgetAmount, null);
            if (amount <= 0 || !Money.IsInRange(amount))
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

                await LoadBudgetsCommand.ExecuteAsync(null);

                await _dialogService.ToastAsync(AppResources.BudgetAddedSuccessfully);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToSaveBudget);
            }
        }

        [RelayCommand]
        private async Task DeleteBudgetAsync(BudgetProgress budget)
        {
            if (budget == null) return;

            var confirm = await _dialogService.ConfirmAsync(
                AppResources.ConfirmDelete,
                string.Format(AppResources.DeleteBudgetConfirmation, budget.CategoryName),
                AppResources.YesDelete,
                AppResources.Cancel);

            if (!confirm) return;

            try
            {
                await _budgetRepository.DeleteByIdAsync(budget.BudgetId);
                await LoadBudgetsCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToDeleteBudget);
            }
        }

        [RelayCommand]
        private async Task EditBudgetAsync(BudgetProgress budget)
        {
            if (budget == null) return;

            // Simple edit using prompt - could be enhanced with a proper edit form
            var result = await _dialogService.PromptAsync(
                AppResources.EditBudget,
                string.Format(AppResources.EnterNewBudgetLimit, budget.CategoryName),
                AppResources.Save,
                AppResources.Cancel,
                initialValue: budget.LimitAmount.ToString("N2"),
                numeric: true);

            if (string.IsNullOrEmpty(result)) return;

            var newAmount = AmountParser.Parse(result, null);
            if (newAmount <= 0 || !Money.IsInRange(newAmount))
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterValidAmount);
                return;
            }

            try
            {
                await _budgetRepository.UpdateAmountAsync(budget.BudgetId, newAmount);
                await LoadBudgetsCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToSaveBudget);
            }
        }

        partial void OnSelectedPeriodChanged(PeriodItem value)
        {
            IsCustomPeriodSelected = value?.Type == PeriodType.CustomPeriod;
            LoadBudgetsCommand.Execute(null);
        }

        partial void OnCustomStartDateChanged(DateTime value)
        {
            if (IsCustomPeriodSelected)
            {
                LoadBudgetsCommand.Execute(null);
            }
        }

        partial void OnCustomEndDateChanged(DateTime value)
        {
            if (IsCustomPeriodSelected)
            {
                LoadBudgetsCommand.Execute(null);
            }
        }
    }
}
