using SQLite;

namespace MoneyRecord.Models
{
    /// <summary>
    /// One synced row's sync data (Stage 26): the global id other devices know it by, and when and where it last changed. Kept in
    /// this side table so the data tables stay as they are. Empty while sync is off.
    /// </summary>
    public class SyncMeta
    {
        [PrimaryKey]
        public string GlobalId { get; set; } = string.Empty;

        [Indexed(Name = "IX_SyncMeta_Row", Order = 1)]
        public string TableName { get; set; } = string.Empty;

        /// <summary>The row's ID in its table (0 = a delete of a row this device never had).</summary>
        [Indexed(Name = "IX_SyncMeta_Row", Order = 2)]
        public int LocalId { get; set; }

        /// <summary>Hybrid logical clock of the last change (milliseconds since 1970 shifted by 16 bits, plus a counter).</summary>
        public long Clock { get; set; }

        public string DeviceId { get; set; } = string.Empty;

        public bool Deleted { get; set; }

        /// <summary>Changed here and not sent to the other devices yet.</summary>
        [Indexed(Name = "IX_SyncMeta_Dirty")]
        public bool Dirty { get; set; }

        /// <summary>The last values of a deleted row as JSON (column name to value, foreign keys still local IDs), so a delete that turns out wrong can be undone.</summary>
        public string? Payload { get; set; }
    }

    /// <summary>What the triggers saw change since the last <c>SyncStateService.MaterializeAsync</c>.</summary>
    public class SyncDirty
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string TableName { get; set; } = string.Empty;

        public int LocalId { get; set; }

        public bool Deleted { get; set; }

        /// <summary>For a delete: the row as it was, as JSON.</summary>
        public string? Payload { get; set; }
    }

    /// <summary>The last change file of another device that was applied here.</summary>
    public class SyncPeerState
    {
        [PrimaryKey]
        public string DeviceId { get; set; } = string.Empty;

        public long LastSeq { get; set; }
    }

    /// <summary>The single row (Id 1) with this database's sync settings.</summary>
    public class SyncState
    {
        [PrimaryKey]
        public int Id { get; set; } = 1;

        public bool Enabled { get; set; }

        /// <summary>Set while the sync engine writes other devices' changes, so the triggers don't count them as local edits.</summary>
        public bool Applying { get; set; }

        /// <summary>Random per database, new after a restore, so two devices never share one.</summary>
        public string DeviceId { get; set; } = string.Empty;

        public long LastClock { get; set; }

        /// <summary>Hash of the trigger SQL installed, to reinstall them when a column is added.</summary>
        public string TriggerHash { get; set; } = string.Empty;
    }
}
