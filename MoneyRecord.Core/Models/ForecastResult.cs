using MoneyRecord.Helpers;

namespace MoneyRecord.Models
{
    /// <summary>One account's forecast, in its own currency (resolved ISO code).</summary>
    public sealed record AccountForecast(int AccountId, string AccountName, string CurrencyCode, ForecastSeries Series);

    /// <summary>
    /// The total of one currency's accounts, or (with <paramref name="IsApproximate"/>) every account converted into the base
    /// currency, each event at its own day's rate.
    /// </summary>
    public sealed record CurrencyForecast(string CurrencyCode, ForecastSeries Series, bool IsApproximate = false);

    /// <summary>
    /// The balances over the next <paramref name="Days"/> days. <paramref name="Currencies"/> are totals per currency (base
    /// first); <paramref name="Converted"/> is set only when the "Show in" switch converts, and then
    /// <paramref name="NotConverted"/> lists, per currency, what had no rate and was left out (signed, net).
    /// <paramref name="Events"/> are the ones behind the lines, soonest first, for the user's own accounts.
    /// </summary>
    public sealed record ForecastResult(DateTime Today, int Days, IReadOnlyList<AccountForecast> Accounts,
        IReadOnlyList<CurrencyForecast> Currencies, CurrencyForecast? Converted, IReadOnlyList<CurrencyAmount> NotConverted,
        IReadOnlyList<ForecastEvent> Events);
}
