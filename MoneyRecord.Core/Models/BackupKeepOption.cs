using MoneyRecord.Helpers;
using MoneyRecord.Services;

namespace MoneyRecord.Models
{
    /// <summary>A Settings choice for how many automatic backups are kept: "5 backups", named in the current language.</summary>
    public sealed record BackupKeepOption(int Count, string Name)
    {
        public static IReadOnlyList<BackupKeepOption> All() =>
            AutoBackupSettings.KeepChoices.Select(count => new BackupKeepOption(count, Plurals.Backups(count))).ToList();
    }
}
