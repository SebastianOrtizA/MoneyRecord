namespace MoneyRecord.Services.Interfaces
{
    /// <summary>The sync data of this database (Stage 26): switching sync on and off and turning local edits into rows to send.</summary>
    public interface ISyncStateService
    {
        Task<bool> IsEnabledAsync();

        /// <summary>This database's device id; empty while sync is off.</summary>
        Task<string> GetDeviceIdAsync();

        /// <summary>Starts noticing changes and marks every existing row as new (for the first snapshot). A no-op when already on.</summary>
        Task EnableAsync();

        /// <summary>Stops noticing changes and forgets all sync data (the data rows stay).</summary>
        Task DisableAsync();

        /// <summary>
        /// Gives every row changed since the last call a global id (new rows) or a delete mark and a new clock tick, ready to send.
        /// Returns how many rows were marked.
        /// </summary>
        Task<int> MaterializeAsync();
    }
}
