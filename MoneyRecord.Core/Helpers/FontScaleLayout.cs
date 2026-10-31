namespace MoneyRecord.Helpers
{
    /// <summary>
    /// How much room things get when the user enlarges the system font: a figure or a name in a narrow cell has to stay
    /// whole, so there are fewer cells per row (a text that doesn't fit is wrong, not just ugly).
    /// </summary>
    public static class FontScaleLayout
    {
        /// <summary>The scale from which Home's three period tiles stack in one column and categories show two per row.</summary>
        public const double Large = 1.5;

        /// <summary>The scale from which categories show three per row.</summary>
        public const double Larger = 1.15;

        /// <summary>Category cells per row on the add form: 4 at the normal font, 3 a little larger, 2 when large.</summary>
        public static int CategoryColumns(double fontScale) => fontScale >= Large ? 2 : fontScale >= Larger ? 3 : 4;

        /// <summary>Income, Expenses and Net go one under the other: side by side an amount like "-$1,405.50" doesn't fit.</summary>
        public static bool StacksPeriodTiles(double fontScale) => fontScale >= Large;
    }
}
