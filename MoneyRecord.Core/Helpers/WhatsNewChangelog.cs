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

            // A neutral "es" has no region: Spain's text, like the language fallback elsewhere
            var region = culture.IsNeutralCulture ? string.Empty : new RegionInfo(culture.Name).TwoLetterISORegionName;
            return region.Length == 0 || region.Equals("ES", StringComparison.OrdinalIgnoreCase) ? "es-ES" : "es-419";
        }

        /// <summary>The asset path of a store locale's text.</summary>
        public static string AssetPath(string locale) => $"{Folder}/{locale}.txt";
    }
}
