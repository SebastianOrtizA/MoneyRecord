using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class ExchangeRateSettings(IPreferencesService preferences) : IExchangeRateSettings
    {
        private const string EnabledKey = "ExchangeRates.Enabled";
        private const string LastUpdatedKey = "ExchangeRates.LastUpdated";

        public bool Enabled
        {
            get => preferences.Get(EnabledKey, false);
            set => preferences.Set(EnabledKey, value);
        }

        public DateTime? LastUpdated
        {
            get => preferences.Get(LastUpdatedKey, 0L) is > 0 and var ticks ? new DateTime(ticks) : null;
            set => preferences.Set(LastUpdatedKey, value?.Ticks ?? 0L);
        }
    }
}
