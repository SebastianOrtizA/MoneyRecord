namespace MoneyRecord.Controls
{
    /// <summary>The one slide-up animation for the in-page bottom sheets (add entry, transaction details, account form).</summary>
    public static class SheetAnimation
    {
        /// <summary>
        /// Moves the sheet below the screen right away (before it is drawn in place), then slides it up. On the first
        /// open it isn't measured yet (Height 0), so it starts <paramref name="fallbackHeight"/> below.
        /// </summary>
        public static Task SlideUpAsync(this VisualElement sheet, double fallbackHeight)
        {
            sheet.TranslationY = sheet.Height > 0 ? sheet.Height : fallbackHeight;
            return sheet.TranslateToAsync(0, 0, 200, Easing.CubicOut);
        }
    }
}
