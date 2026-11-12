namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// What still points at an account.
    /// </summary>
    public sealed record AccountUsage(int Transactions, int Transfers, int RecurringRules);

    /// <summary>
    /// What deleting an account would move into the default account: how many transactions and recurring rules, the net
    /// total of the transactions (income minus expenses, in cents of the account's currency) and both currencies.
    /// </summary>
    public sealed record AccountMove(int Transactions, int RecurringRules, long NetCents, string DefaultAccountName,
        string FromCurrency, string ToCurrency)
    {
        /// <summary>Something moves and the two currencies differ: the amounts need a rate.</summary>
        public bool NeedsRate => (Transactions > 0 || RecurringRules > 0)
            && !string.Equals(FromCurrency, ToCurrency, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Account operations that span several tables.
    /// </summary>
    public interface IAccountService
    {
        Task<AccountUsage> GetUsageAsync(int accountId);

        /// <summary>What deleting the account moves into the default account; null when there is no default account.</summary>
        Task<AccountMove?> GetMoveAsync(int accountId);

        /// <summary>
        /// In one database transaction: moves the account's transactions and recurring rules to the default
        /// account, unlinks it from its savings goal, then deletes it. When the default account is in another
        /// currency the amounts are converted with <paramref name="rate"/> (how many units of the default account's
        /// currency one unit of this account's is worth); the user's own conversions on the transactions no longer apply
        /// and are cleared.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The account is the default one, or transfers still use it (moving them could make a transfer from an
        /// account to itself, so the user has to delete them first), or it has transactions or recurring rules, the
        /// default account is in another currency and there is no positive <paramref name="rate"/> to convert them.
        /// </exception>
        Task DeleteAsync(int accountId, decimal? rate = null);
    }
}
