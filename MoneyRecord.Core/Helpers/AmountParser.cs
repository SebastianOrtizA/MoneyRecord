using MoneyRecord.Services;
using System.Globalization;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// Reads amounts typed or shown in a currency ("$1,250.50", "S/.12,50", "¥1,500"). Used by view models and
    /// CurrencyMaskBehavior.
    /// </summary>
    public static class AmountParser
    {
        /// <summary>
        /// Parses an amount typed in <paramref name="currencyCode"/> (empty = the base currency), rounded to the
        /// currency's decimals.
        /// </summary>
        public static decimal Parse(string? text, string? currencyCode, bool allowNegative = false)
        {
            var decimals = CurrencyService.GetMinorUnits(MoneyFormatter.ResolveCurrencyCode(currencyCode));
            return ParseCurrencyValue(text, decimals, allowNegative, currencyCode);
        }

        /// <summary>
        /// Static helper method to extract the numeric value from a formatted currency string.
        /// Pass the amount's currency so its symbol
        /// (e.g. the "." in "S/.") is not mistaken for a decimal separator.
        /// </summary>
        public static decimal ParseCurrencyValue(string? text, int decimalPlaces = 2, bool allowNegative = false, string? currencyCode = null)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0m;

            var culture = CultureInfo.CurrentCulture;
            var decimalSep = culture.NumberFormat.CurrencyDecimalSeparator;
            var groupSep = culture.NumberFormat.CurrencyGroupSeparator;
            var negativeSign = culture.NumberFormat.NegativeSign;

            // Drop the currency symbol first: some contain separators ("S/.")
            var symbol = CurrencyService.GetSymbol(MoneyFormatter.ResolveCurrencyCode(currencyCode));
            text = text.Replace(symbol, string.Empty).Replace(culture.NumberFormat.CurrencySymbol, string.Empty);

            // Check for negative value indicators
            bool isNegative = allowNegative &&
                (text.Contains(negativeSign) || text.Contains('(') || text.Contains(')'));

            // Remove group separators, keep only digits and decimal separator
            var cleaned = new System.Text.StringBuilder();
            var hasDecimal = false;
            var decimalDigits = 0;

            foreach (var c in text)
            {
                // Skip group separators (thousand separators)
                if (c.ToString() == groupSep)
                    continue;

                // Skip negative sign and parentheses (handled separately)
                if (c.ToString() == negativeSign || c == '(' || c == ')')
                    continue;

                if (char.IsDigit(c))
                {
                    if (hasDecimal)
                    {
                        // Limit decimal places
                        if (decimalDigits < decimalPlaces)
                        {
                            cleaned.Append(c);
                            decimalDigits++;
                        }
                    }
                    else
                    {
                        cleaned.Append(c);
                    }
                }
                else if (c.ToString() == decimalSep && !hasDecimal)
                {
                    // Only the culture's decimal separator marks the decimal point
                    cleaned.Append(decimalSep);
                    hasDecimal = true;
                }
                else if ((c == '.' || c == ',') && !hasDecimal && c.ToString() != groupSep)
                {
                    // Handle manual input with . or , as decimal (when different from group separator)
                    cleaned.Append(decimalSep);
                    hasDecimal = true;
                }
            }

            if (decimal.TryParse(cleaned.ToString(), NumberStyles.Number, culture, out var result))
            {
                var roundedResult = Math.Round(result, decimalPlaces);
                return isNegative ? -roundedResult : roundedResult;
            }

            return 0m;
        }
    }
}
