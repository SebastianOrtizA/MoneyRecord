namespace MoneyRecord.Services.Interfaces
{
    /// <summary>Asks for a new backup password (with confirmation) and saves it; shared by Settings and the one-time offer.</summary>
    public interface IBackupPasswordPrompt
    {
        /// <summary>True when a password was saved; false when the user cancelled.</summary>
        Task<bool> SetAsync();
    }
}
