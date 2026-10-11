using MoneyRecord.Services;
using System.Globalization;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// Formats money amounts with the culture's currency format, always putting the minus sign
    /// before the currency symbol (-$50.00, never ($50.00) or $-50.00).
    /// </summary>
    public static class MoneyFormatter
    {
        public static string Format(decimal amount, CultureInfo? culture = null)
        {
            culture ??= CultureInfo.CurrentCulture;
            var format = (NumberFormatInfo)culture.NumberFormat.Clone();

            // Neutral cultures have no currency; "¤" is the generic placeholder.
            if (format.CurrencySymbol == "¤")
                format.CurrencySymbol = CurrencyService.GetSymbol(CurrencyService.GetDeviceCurrencyCode());

            format.CurrencyNegativePattern = GetLeadingSignPattern(format.CurrencyPositivePattern);
            return amount.ToString("C", format);
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
