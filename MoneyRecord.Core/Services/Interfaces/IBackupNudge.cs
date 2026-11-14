namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// A one-time offer to protect backups with a password, shown after setup and after the first backup the user makes.
    /// </summary>
    public interface IBackupNudge
    {
        /// <summary>
        /// Asks once, when this platform can keep a password and none is set; "Set password" continues into the usual password
        /// dialog. Never throws: it is only a suggestion.
        /// </summary>
        Task OfferAsync();
    }
}
