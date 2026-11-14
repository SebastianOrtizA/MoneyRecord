namespace MoneyRecord.Helpers
{
    /// <summary>
    /// How long wrong PIN tries lock the pad: 5 free tries, then 30 s, 1 min, 5 min, 15 min and 1 h for every try after that.
    /// </summary>
    public static class LockoutPolicy
    {
        public const int FreeTries = 5;

        private static readonly TimeSpan[] Delays =
            [TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1)];

        /// <summary>The wait after the <paramref name="failures"/>-th wrong try in a row (zero while the free tries last).</summary>
        public static TimeSpan DelayAfter(int failures) =>
            failures < FreeTries ? TimeSpan.Zero : Delays[Math.Min(failures - FreeTries, Delays.Length - 1)];

        /// <summary>Tries left before the first wait (zero once the free ones are used).</summary>
        public static int FreeTriesLeft(int failures) => Math.Max(0, FreeTries - failures);
    }
}
