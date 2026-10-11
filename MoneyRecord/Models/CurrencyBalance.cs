namespace MoneyRecord.Models
{
    /// <summary>
    /// The total balance of all accounts in one currency (amounts in different currencies are never added together).
    /// </summary>
    public sealed record CurrencyBalance(string CurrencyCode, decimal Balance, int AccountCount);
}
