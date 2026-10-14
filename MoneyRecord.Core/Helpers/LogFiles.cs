using System.IO.Compression;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// The app's log files in <see cref="Services.Interfaces.IAppPaths.LogsFolder"/>: one per day
    /// (<c>moneyrecord-20261014.log</c>), the last <see cref="RetainedDays"/> kept. Settings shares them as one zip.
    /// </summary>
    public static class LogFiles
    {
        public const int RetainedDays = 7;

        /// <summary>Serilog's rolling file path: it inserts the date before the extension.</summary>
        public static string RollingPath(string logsFolder) => Path.Combine(logsFolder, "moneyrecord-.log");

        /// <summary>
        /// Zips every log file into <paramref name="zipPath"/>, oldest first. Returns false (and writes nothing) when
        /// there are no logs. Today's file is still open in the logger, so it is read with shared access.
        /// </summary>
        public static bool CreateArchive(string logsFolder, string zipPath)
        {
            var files = Directory.Exists(logsFolder)
                ? Directory.GetFiles(logsFolder, "*.log").Order(StringComparer.Ordinal).ToList()
                : [];
            if (files.Count == 0)
                return false;

            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            foreach (var file in files)
            {
                using var source = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var entry = zip.CreateEntry(Path.GetFileName(file), CompressionLevel.Optimal).Open();
                source.CopyTo(entry);
            }
            return true;
        }
    }
}
