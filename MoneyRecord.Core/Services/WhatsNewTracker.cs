namespace MoneyRecord.Services
{
    /// <summary>
    /// Remembers the version whose release notes were shown, so the "What's new" pop-up appears once after each update.
    /// A new install records its version at the start (nothing to announce), so an install that has not recorded one is an
    /// update from a version before this existed: it sees the notes once.
    /// </summary>
    public sealed class WhatsNewTracker(IPreferencesService preferences)
    {
        public const string LastSeenVersionKey = "WhatsNew.LastSeenVersion";

        /// <summary>True when the notes of <paramref name="version"/> have not been shown on this install.</summary>
        public bool IsUnseen(string version) =>
            !string.Equals(preferences.Get(LastSeenVersionKey, string.Empty), version, StringComparison.Ordinal);

        public void MarkSeen(string version) => preferences.Set(LastSeenVersionKey, version);
    }
}
