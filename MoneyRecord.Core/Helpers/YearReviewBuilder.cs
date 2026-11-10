using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Helpers
{
    /// <summary>The year's biggest single expense.</summary>
    public sealed record BiggestExpense(string Description, string CategoryName, DateTime Date, decimal Amount);

    /// <summary>A month's expenses (1..12).</summary>
    public sealed record MonthSpending(int Month, decimal Amount);

    /// <summary>A tag and how many of the year's expenses carry it.</summary>
    public sealed record TagUse(string Name, int Count);

    /// <summary>
    /// One calendar year in one currency (unsigned amounts). Empty when there is not a single entry.
    /// </summary>
    public sealed record YearReview(
        int Year,
        string CurrencyCode,
        bool IsApproximate,
        decimal Income,
        decimal Expense,
        IReadOnlyList<CategoryAmount> TopCategories,
        IReadOnlyList<PayeeAmount> TopPayees,
        MonthSpending? HighestMonth,
        MonthSpending? LowestMonth,
        BiggestExpense? Biggest,
        TagUse? TopTag,
        int NoSpendDays,
        int DaysCounted)
    {
        public bool HasData => Income > 0 || Expense > 0;

        public decimal Net => Income - Expense;

        /// <summary>The share of the income left over, in percent (negative when more was spent); null without income.</summary>
        public double? SavingsRate => Income > 0 ? (double)(Net / Income * 100) : null;
    }

    /// <summary>Builds the year in review from the year's transactions.</summary>
    public static class YearReviewBuilder
    {
        public const int TopCount = 3;

        /// <param name="transactions">The year's, in the report's currency (unsigned amounts).</param>
        /// <param name="today">The count of no-spend days stops here for the running year.</param>
        public static YearReview Build(int year, IEnumerable<Transaction> transactions, IEnumerable<int> expenseCategoryIds,
            string currencyCode, bool isApproximate, string noPayeeName, DateTime today)
        {
            var inYear = transactions.Where(t => t.Date.Year == year).ToList();
            var expenses = inYear.Where(t => t.Type == TransactionType.Expense).ToList();
            var income = inYear.Where(t => t.Type == TransactionType.Income).Sum(t => Math.Abs(t.Amount));
            var expense = expenses.Sum(t => Math.Abs(t.Amount));
            if (income == 0 && expense == 0)
                return new YearReview(year, currencyCode, isApproximate, 0, 0, [], [], null, null, null, null, 0, 0);

            var categories = CategoryReportBuilder.Build(expenses, CategoryType.Expense, expenseCategoryIds, currencyCode)
                .Categories.Take(TopCount).ToList();
            var payees = PayeeReportBuilder.Build(expenses, TransactionType.Expense, noPayeeName)
                .Payees.Where(p => p.PayeeId != null).Take(TopCount).ToList();

            var months = expenses
                .GroupBy(t => t.Date.Month)
                .Select(g => new MonthSpending(g.Key, g.Sum(t => Math.Abs(t.Amount))))
                .ToList();
            // One month is no comparison
            var highest = months.Count > 1 ? months.OrderByDescending(m => m.Amount).ThenBy(m => m.Month).First() : null;
            var lowest = months.Count > 1 ? months.OrderBy(m => m.Amount).ThenBy(m => m.Month).First() : null;

            var biggest = expenses.OrderByDescending(t => Math.Abs(t.Amount)).ThenBy(t => t.Date).FirstOrDefault();

            var tag = expenses
                .SelectMany(t => TransactionTags.Split(t.Tags))
                .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
                .Select(g => new TagUse(g.First(), g.Count()))
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .FirstOrDefault();

            // A new install has no days before its first entry, and the running year none after today
            var first = inYear.Min(t => t.Date.Date);
            var last = new DateTime(year, 12, 31);
            if (today.Date < last)
                last = today.Date;
            var spendDays = expenses.Select(t => t.Date.Date).Distinct().Count(d => d <= last);
            var counted = last >= first ? (last - first).Days + 1 : 0;

            return new YearReview(year, currencyCode, isApproximate, income, expense, categories, payees, highest, lowest,
                biggest == null ? null : new BiggestExpense(
                    EntryDescription.Resolve(biggest.Description, biggest.CategoryName), biggest.CategoryName, biggest.Date,
                    Math.Abs(biggest.Amount)),
                tag, Math.Max(0, counted - spendDays), counted);
        }
    }
}
