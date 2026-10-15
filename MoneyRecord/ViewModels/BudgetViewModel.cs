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
        private readonly IReminderService _reminders;

        // While the page is shown: a period change (here or on another tab) reloads it
        private IDisposable? _periodWatch;

        [ObservableProperty]
        private bool isLoading = false;

        [ObservableProperty]
        private bool hasBudgets = false;

        [ObservableProperty]
        private ObservableCollection<BudgetProgress> budgets = new();

        /// <summary>The add/edit sheet is open.</summary>
        [ObservableProperty]
        private bool isFormOpen;

        /// <summary>The budget the sheet edits; null when it adds one.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEditMode), nameof(FormTitle), nameof(SaveText))]
        private BudgetProgress? editingBudget;

        public bool IsEditMode => EditingBudget != null;

        public string FormTitle => IsEditMode ? AppResources.EditBudget : AppResources.AddCategoryToBudget;

        public string SaveText => IsEditMode ? AppResources.Save : AppResources.AddBudgetButton;

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

        public List<BudgetPeriodItem> BudgetPeriods { get; } =
        [
            new BudgetPeriodItem { Period = BudgetPeriod.Day },
            new BudgetPeriodItem { Period = BudgetPeriod.Month },
            new BudgetPeriodItem { Period = BudgetPeriod.Year }
        ];

        /// <summary>The shared period (Home, Transactions, Budgets, Spending / Income).</summary>
        public PeriodViewModel Period { get; }

        public BudgetViewModel(PeriodViewModel period, ICategoryRepository categoryRepository, IBudgetRepository budgetRepository, IBudgetSummaryService budgetSummaryService, IErrorHandler errorHandler, IDialogService dialogService,
            IReminderService reminders)
        {
            _categoryRepository = categoryRepository;
            _budgetRepository = budgetRepository;
            _budgetSummaryService = budgetSummaryService;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
            _reminders = reminders;
            selectedBudgetPeriod = BudgetPeriods[1]; // Default to Month
            Period = period;
        }

        /// <summary>The page appeared: load, and follow the shared period until <see cref="Deactivate"/>.</summary>
        public Task InitializeAsync()
        {
            _periodWatch ??= Period.WatchChanges(() => LoadBudgetsCommand.Execute(null));
            return LoadBudgetsCommand.ExecuteAsync(null);
        }

        /// <summary>The page disappeared: stop following the period (it reloads when it appears again).</summary>
        public void Deactivate()
        {
            _periodWatch?.Dispose();
            _periodWatch = null;
        }

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
                    Period.SelectedPeriod?.Type ?? PeriodType.CalendarMonth, Period.CustomStartDate, Period.CustomEndDate);
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

        /// <summary>Opens the sheet for a new budget on the first expense category without one.</summary>
        [RelayCommand]
        private async Task AddBudgetAsync()
        {
            if (AvailableCategories.Count == 0)
            {
                await _dialogService.ToastAsync(AppResources.NoCategoriesLeftForBudget);
                return;
            }

            EditingBudget = null;
            SelectedCategory = AvailableCategories[0];
            SelectedBudgetPeriod = BudgetPeriods[1]; // Month
            BudgetAmount = string.Empty;
            IsFormOpen = true;
        }

        [RelayCommand]
        private void EditBudget(BudgetProgress? budget)
        {
            if (budget == null)
                return;

            EditingBudget = budget;
            SelectedBudgetPeriod = BudgetPeriods.First(p => p.Period == budget.Period);
            BudgetAmount = budget.OriginalLimitAmount.ToString();
            IsFormOpen = true;
        }

        [RelayCommand]
        private void CloseForm()
        {
            IsFormOpen = false;
            EditingBudget = null;
        }

        /// <summary>Closes the add/edit sheet; false when it wasn't open.</summary>
        public bool CloseTopOverlay()
        {
            if (!IsFormOpen)
                return false;

            CloseForm();
            return true;
        }

        [RelayCommand]
        private async Task SaveBudgetAsync()
        {
            if (!IsEditMode && SelectedCategory == null)
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

            var period = SelectedBudgetPeriod?.Period ?? BudgetPeriod.Month;
            var adding = !IsEditMode;
            try
            {
                if (EditingBudget is { } editing)
                {
                    await _budgetRepository.UpdateLimitAsync(editing.BudgetId, amount, period);
                }
                else
                {
                    await _budgetRepository.SaveAsync(new Budget
                    {
                        CategoryId = SelectedCategory!.Id,
                        LimitAmount = amount,
                        Period = period,
                        CreatedDate = DateTime.Now,
                        IsActive = true
                    });
                }

                CloseForm();
                await LoadBudgetsCommand.ExecuteAsync(null);

                if (adding)
                {
                    await _dialogService.ToastAsync(AppResources.BudgetAddedSuccessfully);
                    // A first budget: offer alerts when it nears its limit (once)
                    await _reminders.OfferAsync();
                }
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToSaveBudget);
            }
        }

        /// <summary>Deletes the budget in the sheet; the sheet stays open behind the question, so cancelling returns to it.</summary>
        [RelayCommand]
        private async Task DeleteBudgetAsync()
        {
            var budget = EditingBudget;
            if (budget == null)
                return;

            var confirm = await _dialogService.ConfirmAsync(
                AppResources.ConfirmDelete,
                string.Format(AppResources.DeleteBudgetConfirmation, budget.CategoryName),
                AppResources.YesDelete,
                AppResources.Cancel);

            if (!confirm)
                return;

            try
            {
                await _budgetRepository.DeleteByIdAsync(budget.BudgetId);
                CloseForm();
                await LoadBudgetsCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToDeleteBudget);
            }
        }
    }
}
