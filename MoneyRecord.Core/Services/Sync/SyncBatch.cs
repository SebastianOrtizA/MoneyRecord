using System.Text.Json;

namespace MoneyRecord.Services.Sync
{
    /// <summary>One row's change: its new values (foreign keys as global ids), or a delete.</summary>
    public sealed record SyncChange(string Table, string GlobalId, long Clock, string DeviceId, bool Deleted, Dictionary<string, JsonElement>? Fields);

    /// <summary>
    /// One change file: what a device changed since its previous file (numbered <see cref="Seq"/>), or, with Seq 0, a snapshot of
    /// everything (<see cref="PeerSeqs"/> = the last file of each device that the snapshot already contains).
    /// </summary>
    public sealed record SyncBatch(int Format, int Schema, string DeviceId, long Seq, List<SyncChange> Changes, Dictionary<string, long>? PeerSeqs = null);

    /// <summary>The other device (or file) is from a newer version of the app than this one understands.</summary>
    public sealed class SyncVersionException(string message) : Exception(message);
}
