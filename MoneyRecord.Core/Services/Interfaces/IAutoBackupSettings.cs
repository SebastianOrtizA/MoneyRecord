using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>Settings → Automatic backups: off until the user turns them on and picks a folder.</summary>
    public interface IAutoBackupSettings
    {
        bool Enabled { get; set; }

        BackupFrequency Frequency { get; set; }

        /// <summary>How many automatic backups stay in the folder; older ones are deleted.</summary>
        int Keep { get; set; }

        /// <summary>The last automatic backup written (local time); null = none yet.</summary>
        DateTime? LastBackup { get; set; }

        /// <summary>The data the last backup holds (<see cref="IDataExportService.GetFingerprintAsync"/>).</summary>
        string? LastFingerprint { get; set; }

        /// <summary>When the last attempt failed (local time); null when it succeeded.</summary>
        DateTime? LastFailure { get; set; }
    }
}
