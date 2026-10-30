using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class BackupHistory(IPreferencesService preferences, TimeProvider time) : IBackupHistory
    {
        private const string LastWrittenKey = "Backup.LastWritten";

        public DateTime? LastWritten => preferences.Get(LastWrittenKey, 0L) is > 0 and var ticks ? new DateTime(ticks) : null;

        public void MarkWritten() => preferences.Set(LastWrittenKey, time.GetLocalNow().DateTime.Ticks);
    }
}
