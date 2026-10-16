using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class CurrencyDisplaySettings(IPreferencesService preferences) : ICurrencyDisplaySettings
    {
        private const string ShowInKey = "Currencies.ShowIn";
        private const string Converted = "Converted";
        private const string ByCurrency = "ByCurrency";

        public bool? ShowConverted
        {
            get => preferences.Get(ShowInKey, string.Empty) switch
            {
                Converted => true,
                ByCurrency => false,
                _ => null
            };
            set => preferences.Set(ShowInKey, value switch
            {
                true => Converted,
                false => ByCurrency,
                null => string.Empty
            });
        }
    }
}
