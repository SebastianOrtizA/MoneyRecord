using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>An account, or a pocket of one, that money comes from or goes to (exactly one of the two is set).</summary>
    public sealed record ChallengePlace(int? AccountId, int? PocketId)
    {
        public static ChallengePlace Account(int id) => new(id, null);

        public static ChallengePlace Pocket(int id) => new(null, id);
    }

    /// <summary>A place to pick, with the text to show.</summary>
    public sealed record ChallengePlaceOption(ChallengePlace Place, string Label);

    /// <summary>
    /// Where a challenge's money can go and come from. The destination is fixed in the challenge (an account or one of its
    /// pockets); each day asks for the source. Money moves like this: different accounts = a transfer; a source pocket =
    /// a take-out; a destination pocket = a set-aside. In one account only pocket to pocket (or account to pocket) makes sense.
    /// </summary>
    public static class ChallengeSources
    {
        /// <summary>
        /// False for a route that moves nothing: the same account on both sides unless the money ends in a pocket other
        /// than the one it leaves.
        /// </summary>
        public static bool IsValidRoute(int sourceAccountId, int? sourcePocketId, int destinationAccountId, int? destinationPocketId) =>
            sourceAccountId != destinationAccountId || (destinationPocketId != null && sourcePocketId != destinationPocketId);

        /// <summary>The places the challenge's destination can be: accounts and pockets in <paramref name="currencyCode"/>.</summary>
        public static List<ChallengePlaceOption> Destinations(string currencyCode, IEnumerable<Account> accounts, IEnumerable<PocketSummary> pockets) =>
            Places(currencyCode, accounts, pockets, _ => true);

        /// <summary>The places a day's money can come from, given the fixed destination.</summary>
        public static List<ChallengePlaceOption> Sources(SavingsChallenge challenge, IEnumerable<Account> accounts, IEnumerable<PocketSummary> pockets)
        {
            if (challenge.ToAccountId is not { } toAccount)
                return [];
            return Places(challenge.CurrencyCode, accounts, pockets, place =>
            {
                var pocket = place.PocketId == null ? null : pockets.FirstOrDefault(p => p.PocketId == place.PocketId);
                var account = place.AccountId ?? pocket?.Account.Id ?? 0;
                return IsValidRoute(account, place.PocketId, toAccount, challenge.ToPocketId);
            });
        }

        private static List<ChallengePlaceOption> Places(string currencyCode, IEnumerable<Account> accounts, IEnumerable<PocketSummary> pockets,
            Func<ChallengePlace, bool> allowed)
        {
            var options = new List<ChallengePlaceOption>();
            foreach (var account in accounts.Where(a => MoneyFormatter.SameCurrency(a.CurrencyCode, currencyCode)).OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                var place = ChallengePlace.Account(account.Id);
                if (allowed(place))
                    options.Add(new ChallengePlaceOption(place, account.Name));
            }
            foreach (var pocket in pockets.Where(p => MoneyFormatter.SameCurrency(p.Account.CurrencyCode, currencyCode))
                         .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase))
            {
                var place = ChallengePlace.Pocket(pocket.PocketId);
                if (allowed(place))
                    options.Add(new ChallengePlaceOption(place, $"{pocket.DisplayName} ({pocket.BalanceText})"));
            }
            return options;
        }
    }
}
