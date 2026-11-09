using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>One tag of the report: what the period's entries with it add up to (unsigned).</summary>
    /// <param name="Name">The tag as first seen; null for the "no tag" row.</param>
    public sealed record TagAmount(string? Name, decimal Amount, int Count, double Percent)
    {
        /// <summary>0..1, for the row's bar.</summary>
        public double Share => Percent / 100;

        public string PercentText => $"{Percent:0.#}%";
    }

    /// <summary>
    /// The ranked tags. An entry with several tags counts in full under each, so the shares can add up to more than 100%.
    /// </summary>
    public sealed record TagReport(decimal Total, IReadOnlyList<TagAmount> Tags)
    {
        public bool HasData => Total > 0;
    }

    /// <summary>Ranks the tags of a period's expenses (or incomes) by amount.</summary>
    public static class TagReportBuilder
    {
        /// <param name="transactions">In the report's currency (unsigned amounts).</param>
        public static TagReport Build(IEnumerable<Transaction> transactions, TransactionType type)
        {
            var matching = transactions.Where(t => t.Type == type).ToList();
            var total = matching.Sum(t => Math.Abs(t.Amount));
            if (total == 0)
                return new TagReport(0, []);

            TagAmount Row(string? name, List<Transaction> entries)
            {
                var amount = entries.Sum(t => Math.Abs(t.Amount));
                return new(name, amount, entries.Count, (double)(amount / total * 100));
            }

            var byTag = matching
                .SelectMany(t => TransactionTags.Split(t.Tags).Select(tag => (tag, t)))
                .GroupBy(x => x.tag, StringComparer.OrdinalIgnoreCase)
                .Select(g => Row(g.First().tag, g.Select(x => x.t).ToList()))
                .OrderByDescending(r => r.Amount)
                .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            var untagged = matching.Where(t => TransactionTags.Split(t.Tags).Count == 0).ToList();
            return new TagReport(total, untagged.Count == 0 ? byTag : [.. byTag, Row(null, untagged)]);
        }
    }
}
