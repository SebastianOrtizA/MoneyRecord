using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>One payee of the report: what the period's entries with it add up to (unsigned).</summary>
    /// <param name="PayeeId">Null for the "no payee" row.</param>
    public sealed record PayeeAmount(int? PayeeId, string Name, decimal Amount, int Count, double Percent)
    {
        public decimal AverageTicket => Count == 0 ? 0 : Amount / Count;

        /// <summary>0..1, for the row's bar.</summary>
        public double Share => Percent / 100;

        public string PercentText => $"{Percent:0.#}%";
    }

    public sealed record PayeeReport(decimal Total, IReadOnlyList<PayeeAmount> Payees)
    {
        public bool HasData => Total > 0;
    }

    /// <summary>Ranks the payees of a period's expenses (or incomes) by amount.</summary>
    public static class PayeeReportBuilder
    {
        /// <param name="transactions">In the report's currency (unsigned amounts) with <see cref="Transaction.PayeeName"/> filled in.</param>
        /// <param name="noPayeeName">The label of the row for entries without a payee (always last).</param>
        public static PayeeReport Build(IEnumerable<Transaction> transactions, TransactionType type, string noPayeeName)
        {
            var matching = transactions.Where(t => t.Type == type).ToList();
            var total = matching.Sum(t => Math.Abs(t.Amount));
            if (total == 0)
                return new PayeeReport(0, []);

            PayeeAmount Row(IGrouping<int?, Transaction> g) => new(
                g.Key,
                g.Key == null ? noPayeeName : g.Select(t => t.PayeeName).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? noPayeeName,
                g.Sum(t => Math.Abs(t.Amount)),
                g.Count(),
                (double)(g.Sum(t => Math.Abs(t.Amount)) / total * 100));

            var rows = matching.GroupBy(t => t.PayeeId).Select(Row).ToList();
            var named = rows.Where(r => r.PayeeId != null)
                .OrderByDescending(r => r.Amount).ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase);
            return new PayeeReport(total, [.. named, .. rows.Where(r => r.PayeeId == null)]);
        }
    }
}
