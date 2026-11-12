using System.Globalization;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// The conversion asked when an account is deleted into the default account of another currency: the rate (how many
    /// units of the new currency one unit of the old one is worth) and the final amount (what the moving transactions
    /// are worth in the new currency) are two views of the same number.
    /// </summary>
    public static class AccountMoveRate
    {
        /// <summary>Rates are kept to this many decimals.</summary>
        public const int RateDecimals = 6;

        /// <summary>An amount in cents times the rate, to whole cents (halves away from zero).</summary>
        public static long Convert(long cents, decimal rate) =>
            (long)Math.Round(cents * rate, MidpointRounding.AwayFromZero);

        /// <summary>
        /// What the net total of the moving transactions becomes: its size (the sign is the same as before), in whole units
        /// of the new currency as typed by the user.
        /// </summary>
        public static decimal FinalFor(long netCents, decimal rate) =>
            Money.FromCents(Convert(Math.Abs(netCents), rate));

        /// <summary>
        /// The rate that turns the net total into <paramref name="finalAmount"/> (both sizes, no signs). Null when either is
        /// zero: nothing to divide, or nothing worth anything.
        /// </summary>
        public static decimal? RateFor(long netCents, decimal finalAmount)
        {
            var net = Money.FromCents(Math.Abs(netCents));
            if (net == 0 || finalAmount <= 0)
                return null;

            return Math.Round(finalAmount / net, RateDecimals, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// A rate typed with a decimal point or comma ("128.5" or "128,5"); null when it isn't a positive number. A rate
        /// has no thousands separators, so "128.5" is never read as 1285 in a culture where "." groups digits.
        /// </summary>
        public static decimal? ParseRate(string? text, CultureInfo? culture = null)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var typed = text.Trim();
            const NumberStyles style = NumberStyles.AllowDecimalPoint;
            if (decimal.TryParse(typed, style, culture ?? CultureInfo.CurrentCulture, out var rate)
                || decimal.TryParse(typed, style, CultureInfo.InvariantCulture, out rate)
                || decimal.TryParse(typed.Replace(',', '.'), style, CultureInfo.InvariantCulture, out rate))
                return rate > 0 ? Math.Round(rate, RateDecimals, MidpointRounding.AwayFromZero) : null;

            return null;
        }

        /// <summary>A rate as it is typed back into the box: no group separators, no trailing zeros.</summary>
        public static string FormatRate(decimal rate, CultureInfo? culture = null) =>
            Math.Round(rate, RateDecimals).ToString("0.######", culture ?? CultureInfo.CurrentCulture);
    }
}
