using System.Globalization;
using System.Text;
using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Helpers
{
    /// <summary>A line of a section: what it is and its value.</summary>
    public sealed record YearReviewLine(string Label, string Value);

    public sealed record YearReviewSection(string Title, IReadOnlyList<YearReviewLine> Lines);

    /// <summary>
    /// The year in review as titled sections of label / value lines: the page's cards, the shared text and the PDF all show
    /// these. Without amounts only counts, shares and dates remain (for sharing, and while balances are hidden).
    /// </summary>
    public static class YearReviewSections
    {
        public static IReadOnlyList<YearReviewSection> Build(YearReview review, bool includeAmounts)
        {
            if (!review.HasData)
                return [];

            string Money(decimal amount) =>
                (review.IsApproximate ? MoneyFormatter.ApproximateMark : "") + MoneyFormatter.Format(amount, review.CurrencyCode);
            string WithShare(decimal amount, double percent) => includeAmounts
                ? $"{Money(amount)} ({Percent(percent)})"
                : Percent(percent);
            string Month(MonthSpending m) => includeAmounts
                ? $"{MonthName(review.Year, m.Month)} ({Money(m.Amount)})"
                : MonthName(review.Year, m.Month);

            var sections = new List<YearReviewSection>();

            var overview = new List<YearReviewLine>();
            if (includeAmounts)
            {
                overview.Add(new(AppResources.YearIncome, Money(review.Income)));
                overview.Add(new(AppResources.YearExpenses, Money(review.Expense)));
                overview.Add(new(AppResources.YearNet,
                    (review.IsApproximate ? MoneyFormatter.ApproximateMark : "") +
                    MoneyFormatter.FormatSigned(review.Net, review.CurrencyCode)));
            }
            if (review.SavingsRate is { } rate)
                overview.Add(new(AppResources.YearSavingsRate, Percent(rate)));
            if (overview.Count > 0)
                sections.Add(new(AppResources.YearOverview, overview));

            if (review.HighestMonth != null && review.LowestMonth != null)
                sections.Add(new(AppResources.YearMonths,
                [
                    new(AppResources.YearHighestMonth, Month(review.HighestMonth)),
                    new(AppResources.YearLowestMonth, Month(review.LowestMonth))
                ]));

            if (review.TopCategories.Count > 0)
                sections.Add(new(AppResources.YearTopCategories,
                    [.. review.TopCategories.Select(c => new YearReviewLine(c.CategoryName, WithShare(c.Amount, c.Percentage)))]));

            if (review.TopPayees.Count > 0)
                sections.Add(new(AppResources.YearTopPayees,
                    [.. review.TopPayees.Select(p => new YearReviewLine(p.Name, WithShare(p.Amount, p.Percent)))]));

            if (review.Biggest is { } biggest)
            {
                var what = $"{biggest.Description}, {DateFormatter.Format(biggest.Date, DateStyle.Medium)}";
                sections.Add(new(AppResources.YearBiggestExpense,
                    [new(what, includeAmounts ? Money(biggest.Amount) : biggest.CategoryName)]));
            }

            var habits = new List<YearReviewLine>();
            if (review.DaysCounted > 0)
                habits.Add(new(AppResources.YearNoSpendDays,
                    string.Format(AppResources.YearOfDays, review.NoSpendDays, review.DaysCounted)));
            if (review.TopTag is { } tag)
                habits.Add(new(AppResources.YearTopTag, string.Format(AppResources.YearTagUses, tag.Name, tag.Count)));
            if (habits.Count > 0)
                sections.Add(new(AppResources.YearHabits, habits));

            return sections;
        }

        /// <summary>The plain text version: a title, then each section with its lines.</summary>
        public static string ToText(YearReview review, bool includeAmounts)
        {
            var text = new StringBuilder();
            text.AppendLine(string.Format(AppResources.YearReviewTitle, review.Year));
            foreach (var section in Build(review, includeAmounts))
            {
                text.AppendLine().AppendLine(section.Title);
                foreach (var line in section.Lines)
                    text.AppendLine($"- {line.Label}: {line.Value}");
            }
            return text.ToString().TrimEnd();
        }

        private static string Percent(double percent) => string.Format(CultureInfo.CurrentCulture, "{0:0.#}%", percent);

        private static string MonthName(int year, int month) =>
            CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
                new DateTime(year, month, 1).ToString("MMMM", CultureInfo.CurrentCulture));
    }
}
