using MoneyRecord.Helpers;

namespace MoneyRecord.Models
{
    /// <summary>A pocket with its account and what it holds, as the lists show it.</summary>
    public sealed class PocketSummary(Pocket pocket, Account account, long balanceCents)
    {
        public Pocket Pocket { get; } = pocket;

        public Account Account { get; } = account;

        public int PocketId => Pocket.Id;

        public decimal Balance { get; } = Money.FromCents(balanceCents);

        /// <summary>The pocket's currency: its account's, resolved (never empty).</summary>
        public string CurrencyCode => MoneyFormatter.ResolveCurrencyCode(Account.CurrencyCode);

        public string BalanceText => MoneyFormatter.Format(Balance, CurrencyCode);

        /// <summary>"Bank › Travel".</summary>
        public string DisplayName => $"{Account.Name} › {Pocket.Name}";

        public string DisplayIcon => Pocket.DisplayIcon;
    }
}
