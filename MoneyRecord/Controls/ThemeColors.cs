namespace MoneyRecord.Controls
{
    /// <summary>Colour tokens of <c>Colors.xaml</c> for controls built in code (XAML uses <c>AppThemeBinding</c>).</summary>
    internal static class ThemeColors
    {
        public static Color Get(string key) =>
            Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
                ? color
                : Colors.Gray;

        public static void Apply(Label label, string lightKey, string darkKey) =>
            label.SetAppThemeColor(Label.TextColorProperty, Get(lightKey), Get(darkKey));
    }
}
