namespace MoneyRecord.Services.Interfaces
{
    /// <summary>The "What's new" release notes: shown once after an update, and again from About.</summary>
    public interface IWhatsNewService
    {
        /// <summary>
        /// A new install has nothing new to announce: records the running version as seen. Called at start, while the
        /// first-run setup is still pending.
        /// </summary>
        void SkipForNewInstall();

        /// <summary>Shows the notes of the running version when they were not shown yet (once per version), then records that.</summary>
        Task ShowIfNewAsync();

        /// <summary>Shows the notes of the running version (About).</summary>
        Task ShowAsync();
    }
}
