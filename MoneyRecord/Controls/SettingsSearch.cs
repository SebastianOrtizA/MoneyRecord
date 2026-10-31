namespace MoneyRecord.Controls
{
    /// <summary>
    /// Settings search: the title a card is found by, set on the wrapper around the card. The page hides the wrapper (not the
    /// card, whose own IsVisible binding hides it where the feature isn't supported) when the title doesn't match.
    /// </summary>
    public static class SettingsSearch
    {
        public static readonly BindableProperty TitleProperty =
            BindableProperty.CreateAttached("Title", typeof(string), typeof(SettingsSearch), null);

        public static string? GetTitle(BindableObject view) => (string?)view.GetValue(TitleProperty);

        public static void SetTitle(BindableObject view, string? value) => view.SetValue(TitleProperty, value);
    }
}
