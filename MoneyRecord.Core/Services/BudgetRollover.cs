using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>
    /// What a rollover budget carries into its current day, month or year: the unspent part of the earlier periods, oldest first.
    /// Overspending is never carried, the chain starts in the period the budget was created and looks back at most
    /// <see cref="MaxPeriods"/> periods. One instance per run: it remembers each period's spending, so budgets sharing a period
    /// ask the database once.
    /// </summary>
    public sealed class BudgetRollover(IConversionService conversion)
    {
        public const int MaxPeriods = 12;

        private readonly Dictionary<DateTime, Dictionary<int, BudgetSpent>> _spentByWindow = [];

        /// <summary>The unspent amount that reaches the period containing <paramref name="now"/> (0 without rollover).</summary>
        public async Task<decimal> CarryAsync(Budget budget, DateTime now)
        {
            if (!budget.RolloverUnspent)
                return 0;

            var spent = new List<decimal>();
            foreach (var (start, end) in PreviousWindows(budget.Period, now, budget.CreatedDate))
            {
                if (!_spentByWindow.TryGetValue(start, out var byCategory))
                    _spentByWindow[start] = byCategory = await conversion.GetExpensesByCategoryAsync(start, end);
                spent.Add(byCategory.GetValueOrDefault(budget.CategoryId)?.Counted ?? 0);
            }

            return Carry(budget.LimitAmount, spent);
        }

        /// <summary>Each earlier period's unspent amount added to the next one's limit; <paramref name="spent"/> is oldest first.</summary>
        public static decimal Carry(decimal limit, IEnumerable<decimal> spent)
        {
            var carry = 0m;
            foreach (var amount in spent)
                carry = Math.Max(0, limit + carry - amount);
            return carry;
        }

        /// <summary>The periods before the one containing <paramref name="now"/>, oldest first, from the one the budget was created in.</summary>
        public static List<(DateTime Start, DateTime End)> PreviousWindows(BudgetPeriod period, DateTime now, DateTime created)
        {
            var windows = new List<(DateTime, DateTime)>();
            for (var back = 1; back <= MaxPeriods; back++)
            {
                var (start, end) = DateRangeHelper.GetBudgetWindow(period, period switch
                {
                    BudgetPeriod.Day => now.Date.AddDays(-back),
                    BudgetPeriod.Year => now.Date.AddYears(-back),
                    _ => new DateTime(now.Year, now.Month, 1).AddMonths(-back)
                });
                if (end < created)
                    break;
                windows.Add((start, end));
            }

            windows.Reverse();
            return windows;
        }
    }
}
