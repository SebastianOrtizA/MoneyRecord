namespace MoneyRecord.Models
{
    /// <summary>One chip of the reports' currency row (an ISO code); the selected one is the currency shown.</summary>
    public sealed record CurrencyChoice(string Code, bool IsSelected);
}
