namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// The "Show in" choice, shared by Home, the reports and the budgets: totals converted into the base currency, or
    /// listed by currency.
    /// </summary>
    public interface ICurrencyDisplaySettings
    {
        /// <summary>True = converted, false = by currency, null = not chosen yet (converted once rates exist).</summary>
        bool? ShowConverted { get; set; }
    }
}
