using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>An account or a pocket a goal's sheet offers to link; a pocket is listed under its account.</summary>
    public sealed record GoalLinkOption(Account Account, PocketSummary? Pocket)
    {
        public bool IsPocket => Pocket != null;

        /// <summary>The account ID or the pocket ID, as the goal links it.</summary>
        public int Id => Pocket?.PocketId ?? Account.Id;

        /// <summary>"Bank" or "Bank › Travel".</summary>
        public string Name => Pocket?.DisplayName ?? Account.Name;

        /// <summary>
        /// An account and its own pockets can't both be in a goal (the account's balance already holds the pocket's
        /// money), so ticking one unticks the other kind. Pockets of one account can go together.
        /// </summary>
        public bool ExcludesOther(GoalLinkOption other) => IsPocket != other.IsPocket && Account.Id == other.Account.Id;
    }

    /// <summary>Which accounts and pockets a goal's sheet offers.</summary>
    public static class GoalLinkChoices
    {
        /// <summary>
        /// Each account in the goal's currency (given order) that no other goal has, followed by its pockets that no other
        /// goal has. A pocket is offered even when its account belongs to another goal.
        /// </summary>
        /// <param name="linkedAccounts">Account ID → the goal it belongs to.</param>
        /// <param name="linkedPockets">Pocket ID → the goal it belongs to.</param>
        /// <param name="goalId">The goal being edited (its own links stay offered); null for a new one.</param>
        public static List<GoalLinkOption> Build(IEnumerable<Account> accounts, IEnumerable<PocketSummary> pockets,
            IReadOnlyDictionary<int, int> linkedAccounts, IReadOnlyDictionary<int, int> linkedPockets, int? goalId,
            string currencyCode)
        {
            var pocketList = pockets.ToList();
            var options = new List<GoalLinkOption>();
            foreach (var account in accounts.Where(a => MoneyFormatter.SameCurrency(a.CurrencyCode, currencyCode)))
            {
                if (!linkedAccounts.TryGetValue(account.Id, out var owner) || owner == goalId)
                    options.Add(new GoalLinkOption(account, null));

                options.AddRange(pocketList
                    .Where(p => p.Account.Id == account.Id)
                    .Where(p => !linkedPockets.TryGetValue(p.PocketId, out var pocketOwner) || pocketOwner == goalId)
                    .Select(p => new GoalLinkOption(account, p)));
            }
            return options;
        }
    }
}
