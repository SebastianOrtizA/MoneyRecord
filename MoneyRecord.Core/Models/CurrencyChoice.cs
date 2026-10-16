namespace MoneyRecord.Models
{
    /// <summary>
    /// One chip of the reports' currency row: a currency (an ISO code), or <see cref="IsConverted"/> = every currency
    /// converted into <see cref="Code"/> (the base) with the downloaded rates. The selected one is what the reports show.
    /// </summary>
    public sealed record CurrencyChoice(string Code, bool IsSelected, bool IsConverted = false)
    {
        /// <summary>"COP", or "≈ USD" for the converted choice.</summary>
        public string Label => IsConverted ? Helpers.MoneyFormatter.ApproximateMark + Code : Code;
    }
}
