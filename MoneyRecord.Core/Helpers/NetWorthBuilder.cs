using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>The total balance of the counted accounts at the end of one month.</summary>
    public sealed record NetWorthPoint(DateTime Month, decimal Value)
    {
        /// <summary>"Oct" / "oct", for a chart axis.</summary>
        public string Label => DateFormatter.Format(Month, DateStyle.Month);
    }

    /// <summary>
    /// Net worth over time, worked back from today's balances: a month's value is today's total minus every change
    /// dated after that month. Only accounts in one currency are counted (never add currencies together).
    /// </summary>
    public static class NetWorthBuilder
    {
        /// <param name="currentTotal">Today's balance of the counted accounts (all their entries, future-dated included).</param>
        /// <param name="accountIds">The counted accounts.</param>
        /// <param name="transactions">Every transaction dated after the end of the month before <paramref name="firstMonth"/>.</param>
        /// <param name="transfers">Every transfer dated after the end of the month before <paramref name="firstMonth"/>.</param>
        /// <returns>One point per month from <paramref name="firstMonth"/>, oldest first.</returns>
        public static List<NetWorthPoint> Build(decimal currentTotal, IEnumerable<int> accountIds,
            IEnumerable<Transaction> transactions, IEnumerable<Transfer> transfers, DateTime firstMonth, int months)
        {
            var counted = accountIds.ToHashSet();
            var changes = transactions
                .Where(t => t.AccountId is { } id && counted.Contains(id))
                .Select(t => (t.Date, Amount: t.Type == TransactionType.Income ? Math.Abs(t.Amount) : -Math.Abs(t.Amount)))
                .Concat(transfers.Select(t => (t.Date, Amount:
                    (counted.Contains(t.DestinationAccountId) ? Math.Abs(t.Amount) : 0) -
                    (counted.Contains(t.SourceAccountId) ? Math.Abs(t.Amount) : 0))))
                .ToList();

            firstMonth = new DateTime(firstMonth.Year, firstMonth.Month, 1);
            return Enumerable.Range(0, months)
                .Select(i =>
                {
                    var month = firstMonth.AddMonths(i);
                    var monthEnd = month.AddMonths(1).AddTicks(-1);
                    return new NetWorthPoint(month, currentTotal - changes.Where(c => c.Date > monthEnd).Sum(c => c.Amount));
                })
                .ToList();
        }
    }
}
