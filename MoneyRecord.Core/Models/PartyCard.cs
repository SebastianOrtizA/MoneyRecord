using MoneyRecord.Helpers;
using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Models
{
    /// <summary>A party as the Parties list and the party page show it: date, spent (of the budget), what is left.</summary>
    public sealed class PartyCard(PartySummary summary)
    {
        public PartySummary Summary { get; } = summary;

        public PartyEvent Party => Summary.Party;

        public int PartyId => Party.Id;

        public string Name => Party.Name;

        public string DateText => DateFormatter.Format(Party.Date, DateStyle.Medium);

        public bool HasBudget => Summary.BudgetCents is > 0;

        public string SpentText => Summary.BudgetCents is { } budget
            ? string.Format(AppResources.SpentOfBudget, Format(Summary.TotalSpentCents), Format(budget))
            : Format(Summary.TotalSpentCents);

        /// <summary>For a progress bar (0–100).</summary>
        public double ProgressPercentage => Math.Min(100, Fraction * 100);

        public BudgetLevel Level => Summary.BudgetCents is null ? BudgetLevel.OnTrack : TripCategorySpent.LevelOf(Fraction);

        /// <summary>"$40.00 left" or "$10.00 over"; empty without a budget.</summary>
        public string StatusText => Summary.LeftCents is not { } left
            ? string.Empty
            : left >= 0
                ? string.Format(AppResources.PartyLeft, Format(left))
                : string.Format(AppResources.PartyOver, Format(-left));

        private double Fraction => Summary.BudgetCents is > 0 and var budget ? (double)Summary.TotalSpentCents / budget : 0;

        private string Format(long cents) => MoneyFormatter.Format(Money.FromCents(cents), Party.CurrencyCode);
    }

    /// <summary>A person's (or the pot's) line in the party page: what they are owed or owe.</summary>
    public sealed class PartyBalanceRow(PartyBalance balance, string currencyCode)
    {
        public string Name => balance.ParticipantId == PartyLedger.PotId ? AppResources.PartyPot : balance.Name;

        public bool IsPot => balance.ParticipantId == PartyLedger.PotId;

        public bool IsOwed => balance.BalanceCents > 0;

        public bool Owes => balance.BalanceCents < 0;

        public string BalanceText => balance.BalanceCents switch
        {
            > 0 => string.Format(AppResources.PartyOwed, Format(balance.BalanceCents)),
            < 0 => string.Format(AppResources.PartyOwes, Format(-balance.BalanceCents)),
            _ => AppResources.PartyEven
        };

        /// <summary>Paid · handed in · share, for a person; the pot's hint for the pot.</summary>
        public string DetailsText => IsPot
            ? AppResources.PartyPotHint
            : string.Join(" · ", new[]
            {
                balance.PaidCents > 0 ? string.Format(AppResources.PartyPaidLine, Format(balance.PaidCents)) : null,
                balance.ContributedCents > 0 ? string.Format(AppResources.PartyHandedInLine, Format(balance.ContributedCents)) : null,
                string.Format(AppResources.PartyShareLine, Format(balance.ShareCents))
            }.OfType<string>());

        private string Format(long cents) => MoneyFormatter.Format(Money.FromCents(cents), currencyCode);
    }

    /// <summary>One expense of a party in the party page.</summary>
    public sealed record PartyExpenseRow(PartyExpense Expense, string Title, string Details, string AmountText)
    {
        public int Id => Expense.Id;
    }

    /// <summary>One contribution to a party's pot in the party page.</summary>
    public sealed record PartyContributionRow(PartyContribution Contribution, string Title, string Details, string AmountText)
    {
        public int Id => Contribution.Id;
    }
}
