namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// What still points at an account.
    /// </summary>
    public sealed record AccountUsage(int Transactions, int Transfers, int RecurringRules);

    /// <summary>
    /// Account operations that span several tables.
    /// </summary>
    public interface IAccountService
    {
        Task<AccountUsage> GetUsageAsync(int accountId);

        /// <summary>
        /// In one database transaction: moves the account's transactions and recurring rules to the default
        /// account, then deletes it.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The account is the default one, or transfers still use it (moving them could make a transfer from an
        /// account to itself, so the user has to delete them first), or it has transactions or recurring rules and
        /// the default account is in another currency (moving would change what the amounts are worth).
        /// </exception>
        Task DeleteAsync(int accountId);
    }
}
