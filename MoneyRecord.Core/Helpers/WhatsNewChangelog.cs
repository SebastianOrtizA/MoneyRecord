using System.Globalization;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// Which release-notes text the app shows. The texts are the version history next to the Play store changelogs
    /// (<c>fastlane/metadata/android/{locale}/changelogs/history.txt</c>), bundled as app assets in a <c>Changelog/</c> folder:
    /// newest first, one <c>## 1.2.3</c> heading per version followed by its <c>- change</c> lines.
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

        /// <summary>
        /// The notes of every version after <paramref name="lastSeen"/> up to <paramref name="current"/>, so an update that
        /// skipped versions loses none of them; just the newest version's when nothing was recorded. A single version is its
        /// change lines; any other (several, or the newest one older than the running version) get their version as a heading. Null when there is nothing new.
        /// </summary>
        public static string? Since(string history, string lastSeen, string current)
        {
            var currentVersion = ParseVersion(current);
            var lastSeenVersion = ParseVersion(lastSeen);
            if (currentVersion == null)
                return null;

            var sections = Parse(history)
                .Where(s => s.Version <= currentVersion && (lastSeenVersion == null || s.Version > lastSeenVersion))
                .OrderByDescending(s => s.Version)
                .ToList();
            if (lastSeenVersion == null)
                sections = sections.Take(1).ToList();
            if (sections.Count == 0)
                return null;

            return sections.Count == 1 && sections[0].Version == currentVersion
                ? sections[0].Notes
                : string.Join("\n\n", sections.Select(s => $"{s.Label}\n{s.Notes}"));
        }

        private static IEnumerable<(Version Version, string Label, string Notes)> Parse(string history)
        {
            string? label = null;
            var lines = new List<string>();
            // The closing heading flushes the last section
            foreach (var raw in history.Split('\n').Append("## "))
            {
                var line = raw.Trim();
                if (!line.StartsWith("##", StringComparison.Ordinal))
                {
                    if (line.Length > 0)
                        lines.Add(line);
                    continue;
                }

                if (label != null && ParseVersion(label) is { } version && lines.Count > 0)
                    yield return (version, label, string.Join("\n", lines));
                label = line.TrimStart('#').Trim();
                lines.Clear();
            }
        }

        /// <summary>"0.16.0", or "0.9.0-rc1" as 0.9.0; null when it is not a version.</summary>
        private static Version? ParseVersion(string text) =>
            Version.TryParse(text.Split('-')[0].Trim(), out var version) ? version : null;
    }
}
