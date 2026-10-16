using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Models
{
    /// <summary>How often an automatic backup is made (Settings → Automatic backups).</summary>
    public enum BackupFrequency
    {
        Daily,
        Weekly,
        Monthly
    }

    /// <summary>A Settings choice for <see cref="BackupFrequency"/>, named in the current language.</summary>
    public sealed record BackupFrequencyOption(BackupFrequency Frequency, string Name)
    {
        public static IReadOnlyList<BackupFrequencyOption> All() =>
        [
            new(BackupFrequency.Daily, AppResources.BackupDaily),
            new(BackupFrequency.Weekly, AppResources.BackupWeekly),
            new(BackupFrequency.Monthly, AppResources.BackupMonthly)
        ];
    }
}
