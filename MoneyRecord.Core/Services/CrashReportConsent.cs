using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class CrashReportConsent(IPreferencesService preferences, TimeProvider clock, bool isAvailable)
        : ICrashReportConsent
    {
        public const int AskAfterDays = 7;

        private const string EnabledKey = "CrashReports.Enabled";
        private const string DecidedKey = "CrashReports.Decided";
        private const string FirstUseKey = "CrashReports.FirstUse";

        public bool IsAvailable { get; } = isAvailable;

        public bool Enabled
        {
            get => IsAvailable && preferences.Get(EnabledKey, false);
            set
            {
                preferences.Set(EnabledKey, value);
                // A choice made in Settings answers the question too
                preferences.Set(DecidedKey, true);
            }
        }

        public bool ShouldAsk()
        {
            if (!IsAvailable || preferences.Get(DecidedKey, false))
                return false;

            var now = clock.GetUtcNow().UtcDateTime;
            var firstUse = preferences.Get(FirstUseKey, DateTime.MinValue);
            if (firstUse == DateTime.MinValue)
            {
                preferences.Set(FirstUseKey, now);
                return false;
            }
            return now - firstUse >= TimeSpan.FromDays(AskAfterDays);
        }

        public void Answer(bool enabled) => Enabled = enabled;
    }
}
