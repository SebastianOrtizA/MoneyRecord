using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class BudgetSummaryService : IBudgetSummaryService
    {
        private readonly IBudgetRepository _budgetRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IBalanceService _balanceService;

        public BudgetSummaryService(IBudgetRepository budgetRepository, ICategoryRepository categoryRepository, IBalanceService balanceService)
        {
            _budgetRepository = budgetRepository;
            _categoryRepository = categoryRepository;
            _balanceService = balanceService;
        }

        public async Task<List<BudgetProgress>> GetSummaryAsync(PeriodType period, DateTime customStartDate = default, DateTime customEndDate = default)
        {
            var budgets = await _budgetRepository.GetActiveBudgetsAsync();
            if (budgets.Count == 0)
                return [];

            var categories = (await _categoryRepository.GetAllAsync()).ToDictionary(c => c.Id);
            var (startDate, endDate) = DateRangeHelper.GetDateRange(period, customStartDate, customEndDate);
            var spentByCategory = await _balanceService.GetExpensesByCategoryAsync(startDate, endDate);
            var progressList = new List<BudgetProgress>();

            foreach (var budget in budgets)
            {
                if (!categories.TryGetValue(budget.CategoryId, out var category))
                    continue;

                var progress = new BudgetProgress
                {
                    BudgetId = budget.Id,
                    CategoryId = budget.CategoryId,
                    CategoryName = category.Name,
                    CategoryIconCode = category.IconCode,
                    Period = budget.Period,
                    OriginalLimitAmount = budget.LimitAmount,
                    LimitAmount = BudgetProjectionHelper.CalculateProjectedLimit(
                        budget.LimitAmount, budget.Period, period, customStartDate, customEndDate),
                    SpentAmount = spentByCategory.GetValueOrDefault(budget.CategoryId)
                };
                progress.CalculateProgress();
                progressList.Add(progress);
            }

            return progressList.OrderByDescending(b => b.ProgressPercentage).ToList();
        }
    }
}
