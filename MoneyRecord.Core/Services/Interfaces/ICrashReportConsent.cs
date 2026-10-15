namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// Whether the app may send crash reports (Sentry). Off until the user agrees: asked once, a week after first use,
    /// and changeable in Settings. Builds without a Sentry DSN (local and fork builds) never ask and never send.
    /// </summary>
    public interface ICrashReportConsent
    {
        /// <summary>This build can send reports (CI builds carry the Sentry DSN).</summary>
        bool IsAvailable { get; }

        /// <summary>
        /// The user agreed. Setting it (from the one-time question or Settings) also records that the user decided.
        /// Turning it off stops sending at once; turning it on starts with the next app start.
        /// </summary>
        bool Enabled { get; set; }

        /// <summary>
        /// True once: when reports are available, the user hasn't decided yet, and the app has been in use for
        /// <see cref="CrashReportConsent.AskAfterDays"/> days. Not a pure query: the first call starts that count.
        /// </summary>
        bool ShouldAskNow();
    }
}
