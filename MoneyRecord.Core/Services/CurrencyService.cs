using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using System.Globalization;

namespace MoneyRecord.Services
{
    public class CurrencyService : ICurrencyService
    {
        private static readonly List<Currency> _currencies = new()
        {
            new() { Code = "USD", Symbol = "$", Name = "US Dollar" },
            new() { Code = "EUR", Symbol = "€", Name = "Euro" },
            new() { Code = "GBP", Symbol = "£", Name = "British Pound" },
            new() { Code = "JPY", Symbol = "¥", Name = "Japanese Yen" },
            new() { Code = "CAD", Symbol = "CA$", Name = "Canadian Dollar" },
            new() { Code = "AUD", Symbol = "A$", Name = "Australian Dollar" },
            new() { Code = "CHF", Symbol = "CHF", Name = "Swiss Franc" },
            new() { Code = "CNY", Symbol = "¥", Name = "Chinese Yuan" },
            new() { Code = "INR", Symbol = "₹", Name = "Indian Rupee" },
            new() { Code = "MXN", Symbol = "MX$", Name = "Mexican Peso" },
            new() { Code = "BRL", Symbol = "R$", Name = "Brazilian Real" },
            new() { Code = "ARS", Symbol = "AR$", Name = "Argentine Peso" },
            new() { Code = "COP", Symbol = "COL$", Name = "Colombian Peso" },
            new() { Code = "CLP", Symbol = "CL$", Name = "Chilean Peso" },
            new() { Code = "PEN", Symbol = "S/.", Name = "Peruvian Sol" },
            new() { Code = "KRW", Symbol = "₩", Name = "South Korean Won" },
            new() { Code = "SEK", Symbol = "kr", Name = "Swedish Krona" },
            new() { Code = "NOK", Symbol = "kr", Name = "Norwegian Krone" },
            new() { Code = "DKK", Symbol = "kr", Name = "Danish Krone" },
            new() { Code = "PLN", Symbol = "zł", Name = "Polish Zloty" },
            new() { Code = "TRY", Symbol = "₺", Name = "Turkish Lira" },
            new() { Code = "ZAR", Symbol = "R", Name = "South African Rand" },
            new() { Code = "NZD", Symbol = "NZ$", Name = "New Zealand Dollar" },
            new() { Code = "SGD", Symbol = "S$", Name = "Singapore Dollar" },
            new() { Code = "HKD", Symbol = "HK$", Name = "Hong Kong Dollar" },
        };

        private static readonly Dictionary<string, Currency> _currencyMap =
            _currencies.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);

        public const string BaseCurrencyPreferenceKey = "BaseCurrencyCode";

        private readonly IPreferencesService _preferences;

        public CurrencyService(IPreferencesService preferences)
        {
            _preferences = preferences;
        }

        /// <summary>
        /// Loads the saved base currency into <see cref="MoneyFormatter"/>. Called once at startup, before the database
        /// is opened or any page formats money.
        /// </summary>
        public static void LoadBaseCurrency(IPreferencesService preferences)
        {
            MoneyFormatter.BaseCurrencyCode = preferences.Get(BaseCurrencyPreferenceKey, string.Empty);
        }

        public string GetBaseCurrencyCode() => MoneyFormatter.BaseCurrency;

        public void SetBaseCurrencyCode(string code)
        {
            _preferences.Set(BaseCurrencyPreferenceKey, code);
            MoneyFormatter.BaseCurrencyCode = code;
        }

        public List<Currency> GetAvailableCurrencies() => _currencies;

        public Currency? GetCurrency(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            _currencyMap.TryGetValue(code, out var currency);
            return currency;
        }

        /// <summary>
        /// The currency's symbol; the base currency's for an empty code, the code itself for an unknown currency.
        /// </summary>
        public static string GetSymbol(string? code)
        {
            if (string.IsNullOrEmpty(code))
                code = MoneyFormatter.BaseCurrency;
            return _currencyMap.TryGetValue(code, out var currency) ? currency.Symbol : code;
        }

        /// <summary>
        /// Decimal places shown and accepted for a currency: 0 for JPY, CLP and KRW, otherwise 2.
        /// Storage is always in cents (see <see cref="Money"/>).
        /// </summary>
        public static int GetMinorUnits(string? code) => code?.ToUpperInvariant() switch
        {
            "JPY" or "CLP" or "KRW" => 0,
            _ => 2
        };

        string ICurrencyService.GetSymbol(string code) => GetSymbol(code);

        /// <summary>The currency new accounts start with: the base currency.</summary>
        public string GetDefaultCurrencyCode() => GetBaseCurrencyCode();

        /// <summary>
        /// The currency of the device region (e.g. es-CO → COP), even when the app runs in another
        /// language (a fr-FR device shown in English still gets EUR).
        /// </summary>
        public static string GetDeviceCurrencyCode() => GetCurrencyCode(LocalizationService.DeviceCulture);

        /// <summary>
        /// The currency of a culture's region, or USD when the culture has no region
        /// or its currency is not supported.
        /// </summary>
        public static string GetCurrencyCode(CultureInfo culture)
        {
            if (culture.IsNeutralCulture || string.IsNullOrEmpty(culture.Name))
                return "USD";

            try
            {
                var isoCode = new RegionInfo(culture.Name).ISOCurrencySymbol;
                return _currencyMap.ContainsKey(isoCode) ? isoCode : "USD";
            }
            catch (ArgumentException)
            {
                return "USD";
            }
        }

        public string FormatAmount(decimal amount, string currencyCode)
        {
            return MoneyFormatter.Format(amount, currencyCode);
        }
    }
}
