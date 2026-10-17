using MoneyRecord.Services;
using System.Collections.Concurrent;
using System.Globalization;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// Formats money amounts in a currency, with the culture's separators and symbol placement, always putting the
    /// minus sign before the currency symbol (-$50.00, never ($50.00) or $-50.00).
    /// </summary>
    public static class MoneyFormatter
    {
        /// <summary>
        /// The base currency (Settings): used for amounts that belong to no single account, e.g. totals and reports.
        /// Empty = the device region's currency. Set at startup and when the user changes it.
        /// </summary>
        public static string BaseCurrencyCode { get; set; } = string.Empty;

        /// <summary>
        /// Formats <paramref name="amount"/> in <paramref name="currencyCode"/> (an account's currency; null or empty
        /// = the base currency) using <paramref name="culture"/> (default: the current culture).
        /// </summary>
        public static string Format(decimal amount, string? currencyCode = null, CultureInfo? culture = null)
        {
            var code = ResolveCurrencyCode(currencyCode);
            return Format(amount, code, CurrencyService.GetMinorUnits(code), culture);
        }

        /// <summary>
        /// Like <see cref="Format(decimal, string?, CultureInfo?)"/> with an explicit plus sign on a positive amount
        /// (+$5.00, -$5.00, $0.00): for a net amount where the sign carries the meaning, not only the colour.
        /// </summary>
        public static string FormatSigned(decimal amount, string? currencyCode = null, CultureInfo? culture = null)
        {
            var text = Format(amount, currencyCode, culture);
            return amount > 0 ? (culture ?? CultureInfo.CurrentCulture).NumberFormat.PositiveSign + text : text;
        }

        /// <summary>Like <see cref="Format(decimal, string?, CultureInfo?)"/> without decimals (chart axes).</summary>
        public static string FormatWhole(decimal amount, string? currencyCode = null, CultureInfo? culture = null)
        {
            return Format(amount, ResolveCurrencyCode(currencyCode), 0, culture);
        }

        /// <summary>
        /// The amount with the currency's bare sign and then its code ("$6,000,300.00 COP", never "COL$… COP"), for a
        /// line that tells currencies apart by their code. The sign always leads, whatever the culture's placement
        /// ("$1.401,75 USD" in es-ES, not "1.401,75 $ USD"). A currency whose sign is its code shows it once, where the
        /// culture puts it ("CHF 5.00").
        /// </summary>
        public static string FormatWithCode(decimal amount, string? currencyCode = null, CultureInfo? culture = null)
        {
            var code = ResolveCurrencyCode(currencyCode);
            culture ??= CultureInfo.CurrentCulture;
            var symbol = GetSymbol(code, culture, plain: true);
            if (symbol == code)
                return Format(amount, code, culture);
            var text = FormatWithSymbol(amount, symbol, CurrencyService.GetMinorUnits(code), culture, signFirst: true);
            return $"{text} {code}";
        }

        private static string Format(decimal amount, string code, int decimals, CultureInfo? culture)
        {
            culture ??= CultureInfo.CurrentCulture;
            return FormatWithSymbol(amount, GetSymbol(code, culture, plain: false), decimals, culture);
        }

        private static string FormatWithSymbol(decimal amount, string symbol, int decimals, CultureInfo culture,
            bool signFirst = false)
        {
            var format = (NumberFormatInfo)culture.NumberFormat.Clone();
            if (signFirst)
                format.CurrencyPositivePattern = 0; // $n

            format.CurrencySymbol = symbol;
            format.CurrencyDecimalDigits = decimals;
            format.CurrencyNegativePattern = GetLeadingSignPattern(format.CurrencyPositivePattern);
            return amount.ToString("C", format);
        }

        /// <summary>The currency an amount is shown in: its own, else the base currency, else the device's.</summary>
        public static string ResolveCurrencyCode(string? currencyCode) =>
            ResolveCurrencyCode(currencyCode, !string.IsNullOrEmpty(BaseCurrencyCode) ? BaseCurrencyCode : CurrencyService.GetDeviceCurrencyCode());

        /// <summary>The one rule for an amount's currency: its own code (upper case), else <paramref name="baseCurrencyCode"/>.</summary>
        public static string ResolveCurrencyCode(string? currencyCode, string baseCurrencyCode) =>
            (!string.IsNullOrEmpty(currencyCode) ? currencyCode : baseCurrencyCode).ToUpperInvariant();

        /// <summary>
        /// The base currency as an ISO code: the setting (<see cref="BaseCurrencyCode"/>), else the device region's.
        /// </summary>
        public static string BaseCurrency => ResolveCurrencyCode(null);

        /// <summary>Before an amount (or its caption) converted with downloaded rates: "≈ $1,250.00".</summary>
        public const string ApproximateMark = "≈ ";

        /// <summary>True when two currency codes name the same currency (empty = the base currency; case ignored).</summary>
        public static bool SameCurrency(string? currencyCode, string? otherCurrencyCode) =>
            ResolveCurrencyCode(currencyCode) == ResolveCurrencyCode(otherCurrencyCode);

        /// <summary>The ISO currency of each culture's region (null = none), looked up once per culture.</summary>
        private static readonly ConcurrentDictionary<string, string?> RegionCurrencies = new();

        /// <summary>
        /// The culture's own symbol when the currency is the culture region's currency ("$" for COP in es-CO);
        /// otherwise an unambiguous symbol for the currency ("COL$" for COP in en-US), or with <paramref name="plain"/>
        /// its bare sign ("$"), when the code is shown too.
        /// </summary>
        private static string GetSymbol(string code, CultureInfo culture, bool plain)
        {
            var cultureSymbol = culture.NumberFormat.CurrencySymbol;
            if (cultureSymbol != "¤" && !culture.IsNeutralCulture && !string.IsNullOrEmpty(culture.Name))
            {
                var regionCurrency = RegionCurrencies.GetOrAdd(culture.Name, GetRegionCurrency);
                if (string.Equals(regionCurrency, code, StringComparison.OrdinalIgnoreCase))
                    return cultureSymbol;
            }
            return plain ? CurrencyService.GetPlainSymbol(code) : CurrencyService.GetSymbol(code);
        }

        private static string? GetRegionCurrency(string cultureName)
        {
            try
            {
                return new RegionInfo(cultureName).ISOCurrencySymbol;
            }
            catch (ArgumentException)
            {
                return null; // No region for this culture: the currency's own symbol is used
            }
        }

        /// <summary>
        /// Maps a CurrencyPositivePattern to the CurrencyNegativePattern with the same symbol placement
        /// and a leading minus sign.
        /// </summary>
        private static int GetLeadingSignPattern(int positivePattern) => positivePattern switch
        {
            0 => 1,  // $n   → -$n
            1 => 5,  // n$   → -n$
            2 => 9,  // $ n  → -$ n
            3 => 8,  // n $  → -n $
            _ => 1
        };
    }
}
