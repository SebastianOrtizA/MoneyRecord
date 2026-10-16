using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class AutoBackupSettings(IPreferencesService preferences) : IAutoBackupSettings
    {
        /// <summary>The choices Settings offers for <see cref="Keep"/>.</summary>
        public static readonly IReadOnlyList<int> KeepChoices = [3, 5, 10, 20];

        private const string EnabledKey = "AutoBackup.Enabled";
        private const string FrequencyKey = "AutoBackup.Frequency";
        private const string KeepKey = "AutoBackup.Keep";
        private const string LastBackupKey = "AutoBackup.LastBackup";
        private const string LastFingerprintKey = "AutoBackup.LastFingerprint";
        private const string LastFailureKey = "AutoBackup.LastFailure";

        public bool Enabled
        {
            get => preferences.Get(EnabledKey, false);
            set => preferences.Set(EnabledKey, value);
        }

        public BackupFrequency Frequency
        {
            get => preferences.Get(FrequencyKey, (int)BackupFrequency.Weekly) is var value && Enum.IsDefined((BackupFrequency)value)
                ? (BackupFrequency)value
                : BackupFrequency.Weekly;
            set => preferences.Set(FrequencyKey, (int)value);
        }

        public int Keep
        {
            get => preferences.Get(KeepKey, 5) is > 0 and var keep ? keep : 5;
            set => preferences.Set(KeepKey, value);
        }

        public DateTime? LastBackup
        {
            get => GetTime(LastBackupKey);
            set => SetTime(LastBackupKey, value);
        }

        public string? LastFingerprint
        {
            get => preferences.Get(LastFingerprintKey, string.Empty) is { Length: > 0 } value ? value : null;
            set => preferences.Set(LastFingerprintKey, value ?? string.Empty);
        }

        public DateTime? LastFailure
        {
            get => GetTime(LastFailureKey);
            set => SetTime(LastFailureKey, value);
        }

        private DateTime? GetTime(string key) => preferences.Get(key, 0L) is > 0 and var ticks ? new DateTime(ticks) : null;

        private void SetTime(string key, DateTime? value) => preferences.Set(key, value?.Ticks ?? 0L);
    }
}
