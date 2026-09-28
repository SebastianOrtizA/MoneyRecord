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

        public List<Currency> GetAvailableCurrencies() => _currencies;

        public Currency? GetCurrency(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            _currencyMap.TryGetValue(code, out var currency);
            return currency;
        }

        public static string GetSymbol(string code)
        {
            if (string.IsNullOrEmpty(code)) return "$";
            return _currencyMap.TryGetValue(code, out var currency) ? currency.Symbol : "$";
        }

        string ICurrencyService.GetSymbol(string code) => GetSymbol(code);

        public string GetDefaultCurrencyCode()
        {
            try
            {
                var culture = CultureInfo.CurrentCulture;
                var region = new RegionInfo(culture.Name);
                var isoCode = region.ISOCurrencySymbol;
                return _currencyMap.ContainsKey(isoCode) ? isoCode : "USD";
            }
            catch
            {
                return "USD";
            }
        }

        public string FormatAmount(decimal amount, string currencyCode)
        {
            var symbol = GetSymbol(currencyCode);
            return $"{symbol}{amount:N2}";
        }
    }
}
