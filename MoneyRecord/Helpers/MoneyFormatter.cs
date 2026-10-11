using MoneyRecord.Services;
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

        /// <summary>Like <see cref="Format(decimal, string?, CultureInfo?)"/> without decimals (chart axes).</summary>
        public static string FormatWhole(decimal amount, string? currencyCode = null, CultureInfo? culture = null)
        {
            return Format(amount, ResolveCurrencyCode(currencyCode), 0, culture);
        }

        private static string Format(decimal amount, string code, int decimals, CultureInfo? culture)
        {
            culture ??= CultureInfo.CurrentCulture;
            var format = (NumberFormatInfo)culture.NumberFormat.Clone();

            format.CurrencySymbol = GetSymbol(code, culture);
            format.CurrencyDecimalDigits = decimals;
            format.CurrencyNegativePattern = GetLeadingSignPattern(format.CurrencyPositivePattern);
            return amount.ToString("C", format);
        }

        /// <summary>The currency an amount is shown in: its own, else the base currency, else the device's.</summary>
        public static string ResolveCurrencyCode(string? currencyCode)
        {
            if (!string.IsNullOrEmpty(currencyCode))
                return currencyCode.ToUpperInvariant();
            return !string.IsNullOrEmpty(BaseCurrencyCode) ? BaseCurrencyCode : CurrencyService.GetDeviceCurrencyCode();
        }

        /// <summary>
        /// The culture's own symbol when the currency is the culture region's currency ("$" for COP in es-CO);
        /// otherwise an unambiguous symbol for the currency ("COL$" for COP in en-US).
        /// </summary>
        private static string GetSymbol(string code, CultureInfo culture)
        {
            var cultureSymbol = culture.NumberFormat.CurrencySymbol;
            if (cultureSymbol != "¤" && !culture.IsNeutralCulture && !string.IsNullOrEmpty(culture.Name))
            {
                try
                {
                    if (string.Equals(new RegionInfo(culture.Name).ISOCurrencySymbol, code, StringComparison.OrdinalIgnoreCase))
                        return cultureSymbol;
                }
                catch (ArgumentException)
                {
                    // No region for this culture: fall through to the currency's own symbol
                }
            }
            return CurrencyService.GetSymbol(code);
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
