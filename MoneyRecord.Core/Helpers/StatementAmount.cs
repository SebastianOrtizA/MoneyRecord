using System.Globalization;
using System.Text;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// Signed amounts as bank statements write them: "-1,234.56", "-1.234,56", "$ -12", "(12.00)", "12.00-", "1 234,56 €".
    /// The decimal separator is decided once per column (<see cref="DetectDecimalSeparator"/>): "1.234" alone could be
    /// either, the rest of the column tells.
    /// </summary>
    public static class StatementAmount
    {
        /// <summary>
        /// '.' or ',': the one the column's values use before their last 1–2 digits, or that a value with both uses last.
        /// Undecided (only values like "1,234" or "12") → the current culture's, when it is one of the two, else '.'.
        /// </summary>
        public static char DetectDecimalSeparator(IEnumerable<string> values)
        {
            int dot = 0, comma = 0;
            foreach (var value in values)
            {
                switch (Vote(value))
                {
                    case '.': dot++; break;
                    case ',': comma++; break;
                }
            }
            if (dot != comma)
                return dot > comma ? '.' : ',';
            return CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator == "," ? ',' : '.';
        }

        /// <summary>
        /// Enough of a column's values for it to be a date or amount column: all but one (or 20% of a long one), and at
        /// least two (a one-row file: that row). A statement's totals or "opening balance" line then shows up in the
        /// preview as unreadable instead of hiding the column.
        /// </summary>
        public static bool IsMost(int matching, int total) =>
            matching == total ? total > 0 : matching >= 2 && total - matching <= Math.Max(1, total / 5);

        private static char? Vote(string value)
        {
            int lastDot = value.LastIndexOf('.'), lastComma = value.LastIndexOf(',');
            if (lastDot >= 0 && lastComma >= 0)
                return lastDot > lastComma ? '.' : ',';

            var separator = lastDot >= 0 ? '.' : lastComma >= 0 ? ',' : (char?)null;
            if (separator is not { } sep)
                return null;
            if (value.Count(c => c == sep) > 1)
                return sep == '.' ? ',' : '.'; // "1.234.567": a thousands separator
            var digitsAfter = value[(value.LastIndexOf(sep) + 1)..].TakeWhile(char.IsDigit).Count();
            return digitsAfter is 1 or 2 ? sep : null; // "1,234" is undecided
        }

        /// <summary>
        /// Reads a signed amount with <paramref name="decimalSeparator"/>; currency signs, codes and spaces are skipped.
        /// Fails on text without digits or with anything else (a date, two numbers).
        /// </summary>
        public static bool TryParse(string text, char decimalSeparator, out decimal amount)
        {
            amount = 0;
            var value = text.Trim();
            var negative = false;
            if (value.StartsWith('(') && value.EndsWith(')'))
            {
                negative = true;
                value = value[1..^1];
            }

            var thousands = decimalSeparator == '.' ? ',' : '.';
            var number = new StringBuilder();
            var signSeen = false;
            var digitsEnded = false;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (char.IsDigit(c))
                {
                    if (digitsEnded)
                        return false; // "12 - 34"
                    number.Append(c);
                }
                else if (c == decimalSeparator)
                {
                    if (number.ToString().Contains('.') || digitsEnded)
                        return false;
                    number.Append('.');
                }
                else if (c == thousands || c == '\'' || c == '\u00A0' || c == '\u202F' || (c == ' ' && number.Length > 0))
                {
                    // grouping (a space counts only between digits; before them it's just spacing)
                }
                else if (c is '-' or '\u2212' or '+')
                {
                    if (signSeen)
                        return false;
                    signSeen = true;
                    negative |= c != '+';
                    if (number.Length > 0)
                        digitsEnded = true; // a trailing sign: "12.00-"
                }
                else if (char.IsLetter(c))
                {
                    // A currency code stands apart ("USD 12", "12 COP"); "REF1" or "12AB" is a reference, not an amount
                    if (i > 0 && char.IsDigit(value[i - 1]) || i + 1 < value.Length && char.IsDigit(value[i + 1]))
                        return false;
                    if (number.Length > 0)
                        digitsEnded = true;
                }
                else if (char.GetUnicodeCategory(c) == UnicodeCategory.CurrencySymbol || char.IsWhiteSpace(c))
                {
                    if (number.Length > 0)
                        digitsEnded = true; // "1 234,56 €", "12.00 USD"
                }
                else
                {
                    return false;
                }
            }

            if (!number.ToString().Any(char.IsDigit)
                || !decimal.TryParse(number.ToString(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed))
                return false;
            amount = negative ? -parsed : parsed;
            return true;
        }
    }
}
