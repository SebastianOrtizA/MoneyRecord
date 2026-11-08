using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class BudgetSummaryService : IBudgetSummaryService
    {
        private readonly IBudgetRepository _budgetRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IConversionService _conversion;

        public BudgetSummaryService(IBudgetRepository budgetRepository, ICategoryRepository categoryRepository, IConversionService conversion)
        {
            _budgetRepository = budgetRepository;
            _categoryRepository = categoryRepository;
            _conversion = conversion;
        }

        public async Task<List<BudgetProgress>> GetSummaryAsync(PeriodType period, DateTime customStartDate = default, DateTime customEndDate = default)
        {
            var budgets = await _budgetRepository.GetActiveBudgetsAsync();
            if (budgets.Count == 0)
                return [];

            var categories = (await _categoryRepository.GetAllAsync()).ToDictionary(c => c.Id);
            var (startDate, endDate) = DateRangeHelper.GetDateRange(period, customStartDate, customEndDate);
            var spentByCategory = await _conversion.GetExpensesByCategoryAsync(startDate, endDate);
            var progressList = new List<BudgetProgress>();
            var today = DateTime.Now;
            var rollover = new BudgetRollover(_conversion);

            foreach (var budget in budgets)
            {
                if (!categories.TryGetValue(budget.CategoryId, out var category))
                    continue;

                // Rollover only where the shown period is the budget's own one: a month's leftover means nothing in a year or a custom range
                var carry = RolloverMatches(budget.Period, period) ? await rollover.CarryAsync(budget, today) : 0;

                var progress = new BudgetProgress
                {
                    BudgetId = budget.Id,
                    CategoryId = budget.CategoryId,
                    CategoryName = category.Name,
                    CategoryIconCode = category.IconCode,
                    Period = budget.Period,
                    OriginalLimitAmount = budget.LimitAmount,
                    RolloverEnabled = budget.RolloverUnspent,
                    LimitAmount = BudgetProjectionHelper.CalculateProjectedLimit(
                        budget.LimitAmount, budget.Period, period, customStartDate, customEndDate),
                    CarriedOverAmount = carry,
                };
                progress.LimitAmount += carry;
                progress.SetSpent(spentByCategory.GetValueOrDefault(budget.CategoryId) ?? BudgetSpent.None);
                progress.SetDailyAllowance(today, endDate);
                progressList.Add(progress);
            }

            return progressList.OrderByDescending(b => b.ProgressPercentage).ToList();
        }

        /// <summary>Whether the shown period is the budget's own day, calendar month or calendar year.</summary>
        public static bool RolloverMatches(BudgetPeriod budget, PeriodType shown) => (budget, shown) is
            (BudgetPeriod.Day, PeriodType.Today) or (BudgetPeriod.Month, PeriodType.CalendarMonth) or (BudgetPeriod.Year, PeriodType.CalendarYear);
    }
}
