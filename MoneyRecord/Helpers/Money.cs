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
        /// Rounds to the nearest cent (half away from zero, like the v2 migration's ROUND).
        /// </summary>
        public static long ToCents(decimal amount) =>
            (long)Math.Round(amount * CentsPerUnit, MidpointRounding.AwayFromZero);

        public static decimal FromCents(long cents) => cents / (decimal)CentsPerUnit;
    }
}
