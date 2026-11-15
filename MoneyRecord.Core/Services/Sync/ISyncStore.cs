namespace MoneyRecord.Services.Sync
{
    /// <summary>
    /// Where change files are kept: for each device its numbered files. The sync engine reads and writes only the device's own
    /// local store; a link (a shared folder, the local network) copies files between two stores with <see cref="SyncStores"/>.
    /// </summary>
    public interface ISyncStore
    {
        Task<IReadOnlyList<string>> DevicesAsync();

        /// <summary>The highest file number of a device; 0 = none.</summary>
        Task<long> LastSeqAsync(string deviceId);

        /// <summary>The file, or null when there is none.</summary>
        Task<byte[]?> ReadAsync(string deviceId, long seq);

        /// <summary>Writes a file; one that is already there is replaced with the same content (a repeated write).</summary>
        Task WriteAsync(string deviceId, long seq, byte[] data);
    }

    /// <summary>A store whose files can be listed and deleted (compaction): the device's own folder and a shared folder, not the link to another device.</summary>
    public interface IPrunableSyncStore : ISyncStore
    {
        Task<IReadOnlyList<long>> SeqsAsync(string deviceId);

        /// <summary>Deletes a file; no error when it is not there.</summary>
        Task DeleteAsync(string deviceId, long seq);
    }
}
