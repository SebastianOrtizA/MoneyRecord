namespace MoneyRecord.Services.Interfaces
{
    /// <summary>Settings → Exchange rates: off until the user turns the download on (it needs the internet).</summary>
    public interface IExchangeRateSettings
    {
        /// <summary>The app may download exchange rates (at start, in the daily job, and "Update now").</summary>
        bool Enabled { get; set; }

        /// <summary>The last update that got rates for every currency in use (local time); null = never.</summary>
        DateTime? LastUpdated { get; set; }
    }
}
