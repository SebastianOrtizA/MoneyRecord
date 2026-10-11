namespace MoneyRecord.Helpers
{
    /// <summary>
    /// Money is stored as a whole number of cents (hundredths of the currency unit) for every currency,
    /// because SQLite would store a decimal as a lossy REAL. Zero-decimal currencies (JPY, CLP, KRW)
    /// simply store multiples of 100, so changing an account's currency never rescales its amounts.
    /// </summary>
    public static class Money
    {
        public const int CentsPerUnit = 100;

        /// <summary>
        /// Largest amount the app accepts (just under a trillion). Far below where cents would overflow a long.
        /// </summary>
        public const decimal MaxAmount = 999_999_999_999.99m;

        public static bool IsInRange(decimal amount) => Math.Abs(amount) <= MaxAmount;

        /// <summary>
        /// Rounds to the nearest cent, half away from zero.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Beyond <see cref="MaxAmount"/>; validate input with <see cref="IsInRange"/>.</exception>
        public static long ToCents(decimal amount)
        {
            if (!IsInRange(amount))
                throw new ArgumentOutOfRangeException(nameof(amount), amount, $"Amounts are limited to {MaxAmount}.");

            return (long)Math.Round(amount * CentsPerUnit, MidpointRounding.AwayFromZero);
        }

        public static decimal FromCents(long cents) => cents / (decimal)CentsPerUnit;
    }
}
