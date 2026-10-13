using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>One month's total budget (every budgeted category) and what was spent in those categories.</summary>
    public sealed record MonthBudget(DateTime Month, decimal Limit, decimal Spent)
    {
        /// <summary>"Oct" / "oct", for a chart axis.</summary>
        public string Label => DateFormatter.Format(Month, DateStyle.Month);
    }

    /// <summary>Budget vs actual per calendar month, for the categories budgeted in that month only.</summary>
    public static class BudgetVsActualBuilder
    {
        /// <summary>
        /// A budget's limit for one calendar month: a monthly limit as is, a daily one times the month's days, a yearly
        /// one a twelfth.
        /// </summary>
        public static decimal MonthlyLimit(Budget budget, DateTime month) => budget.Period switch
        {
            BudgetPeriod.Day => budget.LimitAmount * DateTime.DaysInMonth(month.Year, month.Month),
            BudgetPeriod.Year => budget.LimitAmount / 12,
            _ => budget.LimitAmount
        };

        /// <returns>One row per month from <paramref name="firstMonth"/>, oldest first.</returns>
        public static List<MonthBudget> Build(IReadOnlyCollection<Budget> budgets, IEnumerable<Transaction> transactions,
            DateTime firstMonth, int months)
        {
            // Expenses per month and category
            var spent = transactions
                .Where(t => t.Type == TransactionType.Expense)
                .GroupBy(t => (Month: new DateTime(t.Date.Year, t.Date.Month, 1), t.CategoryId))
                .ToDictionary(g => g.Key, g => g.Sum(t => Math.Abs(t.Amount)));

            firstMonth = new DateTime(firstMonth.Year, firstMonth.Month, 1);
            return Enumerable.Range(0, months)
                .Select(i =>
                {
                    var month = firstMonth.AddMonths(i);
                    // A budget counts from the month it was created: before that there was no limit to compare with
                    var active = budgets.Where(b => b.CreatedDate < month.AddMonths(1)).ToList();
                    return new MonthBudget(month,
                        active.Sum(b => MonthlyLimit(b, month)),
                        active.Select(b => b.CategoryId).Distinct().Sum(c => spent.GetValueOrDefault((month, c))));
                })
                .ToList();
        }
    }
}
