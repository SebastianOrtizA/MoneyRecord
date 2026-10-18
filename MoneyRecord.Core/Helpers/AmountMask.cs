using System.Globalization;
using System.Text;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// The text of an amount being typed, with the thousands separators put in ("1250" → "1,250") and the caret
    /// kept next to the same digit. Used by CurrencyMaskBehavior; no symbol, no padding zeros.
    /// </summary>
    public static class AmountMask
    {
        /// <summary>
        /// Groups <paramref name="text"/>, the entry's text after an edit. The caret is found from the edit itself
        /// (what follows it is the tail shared with <paramref name="oldText"/>), because the entry's own caret is not
        /// up to date while its text changes. Groups are of three digits.
        /// </summary>
        /// <returns>The grouped text and where the caret goes in it.</returns>
        public static (string Text, int Caret) Group(string text, string? oldText, int decimalPlaces, bool allowNegative, CultureInfo culture)
        {
            var format = culture.NumberFormat;
            var decimalSep = format.CurrencyDecimalSeparator;
            var groupSep = format.CurrencyGroupSeparator;

            oldText ??= string.Empty;
            var limit = Math.Min(oldText.Length, text.Length);
            var prefix = 0;
            while (prefix < limit && oldText[prefix] == text[prefix])
                prefix++;
            var suffix = 0;
            while (suffix < limit - prefix && oldText[oldText.Length - 1 - suffix] == text[text.Length - 1 - suffix])
                suffix++;
            var caret = text.Length - suffix;

            var negative = allowNegative && text.StartsWith(format.NegativeSign, StringComparison.Ordinal);
            var integer = new StringBuilder();
            var fraction = new StringBuilder();
            var hasDecimal = false;

            // Digits and the decimal mark that sit before the caret: the caret goes after the same number of them
            var significantBeforeCaret = 0;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                var kept = false;
                if (char.IsDigit(c))
                {
                    if (!hasDecimal)
                    {
                        integer.Append(c);
                        kept = true;
                    }
                    else if (fraction.Length < decimalPlaces)
                    {
                        fraction.Append(c);
                        kept = true;
                    }
                }
                else if (!hasDecimal && decimalPlaces > 0 &&
                         (c.ToString() == decimalSep || ((c == '.' || c == ',') && c.ToString() != groupSep)))
                {
                    hasDecimal = true;
                    kept = true;
                }

                if (kept && i < caret)
                    significantBeforeCaret++;
            }

            // "007" → "7", but keep a single zero ("0,5")
            var digits = integer.ToString().TrimStart('0');
            if (digits.Length == 0 && integer.Length > 0)
                digits = "0";

            // Leading zeros dropped before the caret no longer count
            var dropped = integer.Length - digits.Length;
            significantBeforeCaret -= Math.Min(dropped, significantBeforeCaret);

            var grouped = new StringBuilder(negative ? format.NegativeSign : string.Empty);
            var newCaret = grouped.Length;
            var counted = 0;

            void Append(string s)
            {
                grouped.Append(s);
                counted++;
                if (counted == significantBeforeCaret)
                    newCaret = grouped.Length;
            }

            for (var i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0)
                    grouped.Append(groupSep);
                Append(digits[i].ToString());
            }

            if (hasDecimal)
            {
                Append(decimalSep);
                foreach (var d in fraction.ToString())
                    Append(d.ToString());
            }

            return (grouped.ToString(), newCaret);
        }
    }
}
