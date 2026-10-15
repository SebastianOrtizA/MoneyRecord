using System.Globalization;

namespace MoneyRecord.Helpers
{
    /// <summary>The exchange rate a cross-currency transfer implies (what arrived ÷ what left).</summary>
    public static class TransferRate
    {
        /// <summary>
        /// "1 USD = 4,012.5 COP": one unit of the currency worth more, so the number is at least 1 and easy to read.
        /// Null when either amount is missing.
        /// </summary>
        public static string? Describe(decimal sent, string? sentCode, decimal received, string? receivedCode,
            CultureInfo? culture = null)
        {
            sent = Math.Abs(sent);
            received = Math.Abs(received);
            if (sent == 0 || received == 0)
                return null;

            var from = MoneyFormatter.ResolveCurrencyCode(sentCode);
            var to = MoneyFormatter.ResolveCurrencyCode(receivedCode);
            return received >= sent
                ? $"1 {from} = {Number(received / sent, culture)} {to}"
                : $"1 {to} = {Number(sent / received, culture)} {from}";
        }

        private static string Number(decimal rate, CultureInfo? culture) =>
            Math.Round(rate, 4).ToString("#,##0.####", culture ?? CultureInfo.CurrentCulture);
    }
}
