namespace MoneyRecord.Converters
{
    /// <summary>The income and expense colours of Colors.xaml for the current theme, so no converter hard-codes a hue.</summary>
    internal static class TypeColors
    {
        public static Color Income => Get("IncomeColor", "IncomeColorDark", Colors.Gray);

        public static Color Expense => Get("ExpenseColor", "ExpenseColorDark", Colors.Gray);

        /// <summary>Income colour for zero or more, expense colour below zero.</summary>
        public static Color ForSign(decimal amount) => amount >= 0 ? Income : Expense;

        private static Color Get(string light, string dark, Color fallback)
        {
            var key = Application.Current?.RequestedTheme == AppTheme.Dark ? dark : light;
            return Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color ? color : fallback;
        }
    }
}
