using System.Globalization;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// Which release-notes text the app shows. The texts are the Play store changelogs
    /// (<c>fastlane/metadata/android/{locale}/changelogs/default.txt</c>), bundled as app assets in a <c>Changelog/</c> folder.
    /// </summary>
    public static class WhatsNewChangelog
    {
        public const string Folder = "Changelog";

        /// <summary>The store locale used when the language has no text of its own.</summary>
        public const string Fallback = "en-US";

        /// <summary>The store locale whose text suits <paramref name="culture"/>: Spain gets es-ES, other Spanish es-419.</summary>
        public static string Locale(CultureInfo culture)
        {
            if (!culture.TwoLetterISOLanguageName.Equals("es", StringComparison.OrdinalIgnoreCase))
                return Fallback;

            // The region is the culture name's last part ("es-CO", "es-419"); read from the name, not RegionInfo, which
            // does not know a UN region like 419. A neutral "es" has none: Spain's text, like the language fallback elsewhere
            var parts = culture.Name.Split('-');
            var region = parts.Length > 1 ? parts[^1] : string.Empty;
            return region.Length == 0 || region.Equals("ES", StringComparison.OrdinalIgnoreCase) ? "es-ES" : "es-419";
        }

        /// <summary>The asset path of a store locale's text.</summary>
        public static string AssetPath(string locale) => $"{Folder}/{locale}.txt";
    }
}
