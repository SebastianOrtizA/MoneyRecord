namespace MoneyRecord.Helpers
{
    /// <summary>
    /// Helper class for Material Design Icon code conversion.
    /// Centralizes icon display logic to avoid code duplication across models.
    /// </summary>
    public static class IconHelper
    {
        private const string DefaultCategoryIconCode = "F0770";
        private const string DefaultAccountIconCode = "F0070";

        /// <summary>
        /// Converts a hex icon code to its displayable Unicode character.
        /// </summary>
        /// <param name="iconCode">The hex code (e.g., "F0770")</param>
        /// <param name="defaultCode">The fallback code if conversion fails</param>
        /// <returns>The displayable icon character</returns>
        public static string GetDisplayIcon(string? iconCode, string defaultCode)
        {
            try
            {
                var code = string.IsNullOrEmpty(iconCode) ? defaultCode : iconCode;
                var codePoint = Convert.ToInt32(code, 16);
                return char.ConvertFromUtf32(codePoint);
            }
            catch
            {
                try
                {
                    var codePoint = Convert.ToInt32(defaultCode, 16);
                    return char.ConvertFromUtf32(codePoint);
                }
                catch
                {
                    return "\uF0770";
                }
            }
        }

        /// <summary>
        /// Gets the displayable icon for a category icon code.
        /// </summary>
        public static string GetCategoryDisplayIcon(string? iconCode)
            => GetDisplayIcon(iconCode, DefaultCategoryIconCode);

        /// <summary>
        /// Gets the displayable icon for an account icon code.
        /// </summary>
        public static string GetAccountDisplayIcon(string? iconCode)
            => GetDisplayIcon(iconCode, DefaultAccountIconCode);
    }
}
