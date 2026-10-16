using Microsoft.Extensions.Logging;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using System.Globalization;

namespace MoneyRecord.Services
{
    public sealed class AutoBackupService(
        IDataExportService export,
        IBackupFolder folder,
        IAutoBackupSettings settings,
        IAppPaths paths,
        TimeProvider clock,
        ILogger<AutoBackupService> logger) : IAutoBackupService
    {
        /// <summary>Automatic backups are named "MoneyRecord_AutoBackup_20261016_090000.zip"; only these are ever deleted.</summary>
        public const string FilePrefix = "MoneyRecord_AutoBackup_";
        public const string FileExtension = ".zip";

        // The daily job and the app start (or Back up now) may run at the same time in the same process
        private readonly SemaphoreSlim _gate = new(1, 1);

        private DateTime Now => clock.GetLocalNow().DateTime;

        /// <summary>
        /// A backup is due on the day after the last one (daily), 7 days after it (weekly) or a month after it (monthly),
        /// whatever its time of day: the daily job runs at about the same hour, a little earlier or later each day.
        /// </summary>
        public static bool IsDue(DateTime? lastBackup, BackupFrequency frequency, DateTime now)
        {
            if (lastBackup is not { } last)
                return true;

            var next = frequency switch
            {
                BackupFrequency.Daily => last.Date.AddDays(1),
                BackupFrequency.Monthly => last.Date.AddMonths(1),
                _ => last.Date.AddDays(7)
            };
            return now >= next;
        }

        public async Task RunIfDueAsync()
        {
            if (!settings.Enabled || !folder.IsSupported || !IsDue(settings.LastBackup, settings.Frequency, Now))
                return;

            await _gate.WaitAsync();
            try
            {
                // The folder calls may block on its provider (a cloud folder)
                await Task.Run(async () =>
                {
                    // Lost access shows in Settings ("choose it again"); nothing to try until then
                    if (folder.Name == null || !IsDue(settings.LastBackup, settings.Frequency, Now))
                        return;

                    // Nothing changed and the last backup is still there: it already holds this data
                    if (await export.GetFingerprintAsync() == settings.LastFingerprint && BackupFiles().Count > 0)
                        return;

                    await BackUpAsync();
                });
            }
            catch (Exception ex)
            {
                settings.LastFailure = Now;
                logger.LogError(ex, "Automatic backup failed");
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task BackUpNowAsync()
        {
            await _gate.WaitAsync();
            try
            {
                await Task.Run(BackUpAsync);
            }
            catch
            {
                settings.LastFailure = Now;
                throw;
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task BackUpAsync()
        {
            // Written in the app's own folder first, then copied: a provider only ever gets a whole file
            var temp = Path.Combine(paths.BackupsFolder, ".auto-backup.partial");
            try
            {
                var fingerprint = await export.ExportBackupAsync(temp);
                var now = Now;
                folder.Write(FilePrefix + now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + FileExtension, temp);

                settings.LastBackup = now;
                settings.LastFingerprint = fingerprint;
                settings.LastFailure = null;
            }
            finally
            {
                File.Delete(temp);
            }

            // The new backup is safe: failing to delete old ones only leaves a few more
            try
            {
                foreach (var old in BackupFiles().OrderByDescending(name => name, StringComparer.Ordinal).Skip(settings.Keep))
                    folder.Delete(old);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete old automatic backups");
            }
        }

        private List<string> BackupFiles() => folder.ListFiles()
            .Where(name => name.StartsWith(FilePrefix, StringComparison.Ordinal)
                && name.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
