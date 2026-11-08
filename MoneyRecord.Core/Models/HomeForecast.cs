using MoneyRecord.Helpers;

namespace MoneyRecord.Models
{
    /// <summary>The Home forecast card: safe to spend and the first day the balance goes below zero, in one currency.</summary>
    public sealed record HomeForecast(string CurrencyCode, SafeToSpendResult SafeToSpend, DateTime? DipDate);
}
