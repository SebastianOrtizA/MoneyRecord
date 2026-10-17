using MoneyRecord.Services;
using System.Globalization;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// Formats money amounts in a currency with the culture's separators and a plain symbol in front of the number, the
    /// minus sign before it, in every language (-$50.00, $1.401,75; never ($50.00), $-50.00, 1.401,75 $ or COL$300).
    /// The code follows when the symbol alone could be another currency ("$500,00 USD" while the base is COP).
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
        /// = the base currency) using <paramref name="culture"/> (default: the current culture). The base currency is
        /// just its symbol; another currency adds its code when its symbol is shared ("$1.200,00 COP" while the base
        /// is USD, but "£5.00").
        /// </summary>
        public static string Format(decimal amount, string? currencyCode = null, CultureInfo? culture = null)
        {
            var code = ResolveCurrencyCode(currencyCode);
            var text = FormatBesideCode(amount, code, culture);
            return code != BaseCurrency && CurrencyService.HasSharedSymbol(code) ? $"{text} {code}" : text;
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

        /// <summary>The symbol and the number without decimals (chart axes, where the chart names the currency).</summary>
        public static string FormatWhole(decimal amount, string? currencyCode = null, CultureInfo? culture = null)
        {
            var code = ResolveCurrencyCode(currencyCode);
            return FormatWithSymbol(amount, CurrencyService.GetSymbol(code), 0, culture);
        }

        /// <summary>
        /// The amount, then always its code ("$6,000,300.00 COP"), for lines that tell currencies apart by their code.
        /// A currency whose symbol is its code shows it once ("CHF 5.00").
        /// </summary>
        public static string FormatWithCode(decimal amount, string? currencyCode = null, CultureInfo? culture = null)
        {
            var code = ResolveCurrencyCode(currencyCode);
            var text = FormatBesideCode(amount, code, culture);
            return CurrencyService.GetSymbol(code) == code ? text : $"{text} {code}";
        }

        /// <summary>
        /// The amount without its code, for one shown next to the code already (an account card's currency chip, an
        /// amount field of that account): "$6.000.300,00".
        /// </summary>
        public static string FormatBesideCode(decimal amount, string? currencyCode = null, CultureInfo? culture = null)
        {
            var code = ResolveCurrencyCode(currencyCode);
            return FormatWithSymbol(amount, CurrencyService.GetSymbol(code), CurrencyService.GetMinorUnits(code), culture);
        }

        private static string FormatWithSymbol(decimal amount, string symbol, int decimals, CultureInfo? culture)
        {
            var format = (NumberFormatInfo)(culture ?? CultureInfo.CurrentCulture).NumberFormat.Clone();

            format.CurrencySymbol = symbol;
            format.CurrencyDecimalDigits = decimals;
            // In front in every culture; a symbol ending in a letter or dot keeps a space from the digits ("CHF 5.00")
            var spaced = char.IsLetter(symbol[^1]) || symbol[^1] == '.';
            format.CurrencyPositivePattern = spaced ? 2 : 0; // "CHF n" / "$n"
            format.CurrencyNegativePattern = spaced ? 9 : 1; // "-CHF n" / "-$n"
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
    }
}
