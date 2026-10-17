using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Helpers
{
    /// <summary>A party as plain text for the share sheet: what it cost, where everybody stands, who still pays whom.</summary>
    public static class PartyShareText
    {
        public static string Build(PartyCard card, IEnumerable<PartyBalanceRow> balances, IEnumerable<PartySettleRow> toSettle)
        {
            var lines = new List<string>
            {
                $"{card.Name} · {card.DateText}",
                card.SpentText
            };
            if (card.HasBudget)
                lines.Add(card.StatusText);

            lines.Add(string.Empty);
            lines.Add(AppResources.PartyBalances);
            lines.AddRange(balances.Select(b => $"{b.Name}: {b.BalanceText}"));

            lines.Add(string.Empty);
            lines.Add(AppResources.PartySettleUp);
            var payments = toSettle.Select(p => p.Text).ToList();
            lines.AddRange(payments.Count > 0 ? payments : [AppResources.PartyAllSettled]);
            return string.Join(Environment.NewLine, lines);
        }
    }
}
