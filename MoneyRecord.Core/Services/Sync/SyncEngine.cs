using System.Reflection;
using System.Text.Json;
using MoneyRecord.Models;
using MoneyRecord.Services.Repositories;
using SQLite;

namespace MoneyRecord.Services.Sync
{
    /// <summary>
    /// Merges the changes of several devices into one database (Stage 26): writes this device's changes as numbered, encrypted change
    /// files into its local store, and applies the other devices' files from there. The rules: last writer wins per row (hybrid
    /// clock, device id breaks ties); a delete is a tombstone that keeps the row's last values; a delete of a row this device still
    /// uses is undone everywhere, and a row that arrives for a deleted parent brings the parent back; a file whose rows have not
    /// arrived yet waits. See the Stage 26.0 prototype tests for the reasoning, and <see cref="SyncStores"/> for moving files.
    /// </summary>
    public sealed class SyncEngine(DatabaseInitializer database, SyncStateService state, byte[] key, TimeProvider? time = null)
    {
        public const int FormatVersion = 1;
        private readonly TimeProvider _time = time ?? TimeProvider.System;

        // ---- Export ----

        /// <summary>Writes the rows changed here since the last export as this device's next change file; returns how many rows.</summary>
        public async Task<int> ExportAsync(ISyncStore store)
        {
            await state.MaterializeAsync();

            var device = "";
            long seq = 0;
            await Run(s => { device = s.Device; seq = s.PeerSeq(device); });

            // A previous export wrote its file and stopped before recording it: that file counts as sent
            while (await store.ReadAsync(device, seq + 1) is { } left)
            {
                var old = Decode(Name(device, seq + 1), left);
                await Run(s => s.MarkExported(old));
                seq = old.Seq;
            }

            List<SyncChange> changes = [];
            var schema = 0;
            await Run(s =>
            {
                schema = s.SchemaVersion;
                changes = s.DirtyChanges();
            });
            if (changes.Count == 0)
                return 0;

            var batch = new SyncBatch(FormatVersion, schema, device, seq + 1, changes);
            await store.WriteAsync(device, batch.Seq, Encode(Name(device, batch.Seq), batch));
            await Run(s => s.MarkExported(batch));
            return changes.Count;
        }

        // ---- Apply ----

        /// <summary>
        /// Applies the next change file of every other device in <paramref name="store"/>, as far as they can be applied; returns how
        /// many files. A file of a newer version of the app throws <see cref="SyncVersionException"/> (nothing of it is applied).
        /// </summary>
        public async Task<int> ApplyAsync(ISyncStore store)
        {
            // Local edits not collected yet have no clock: without this they would lose to (or be undone by) an older remote change
            await state.MaterializeAsync();
            var me = (await StateRow()).DeviceId;
            var applied = 0;
            bool progress;
            do
            {
                progress = false;
                foreach (var device in (await store.DevicesAsync()).Where(d => d != me).Order(StringComparer.Ordinal))
                {
                    long next = 0;
                    await Run(s => next = s.PeerSeq(device) + 1);
                    var data = await store.ReadAsync(device, next);
                    if (data == null)
                    {
                        // A hole: the device compacted (its files before a base file are gone). Only a base file may be jumped to;
                        // any other file after a hole means the missing one is merely late, so wait for it
                        var last = await store.LastSeqAsync(device);
                        while (data == null && ++next <= last)
                            data = await store.ReadAsync(device, next);
                        if (data == null || !Decode(Name(device, next), data).IsBase)
                            continue;
                    }

                    var batch = Decode(Name(device, next), data);
                    var done = false;
                    await Run(s =>
                    {
                        s.CheckVersions(batch);
                        if (!s.CanApply(batch))
                            return; // waits for a row another device has not delivered yet
                        s.ApplyBatch(batch);
                        s.SetPeerSeq(device, next);
                        if (batch.IsBase)
                            s.SetBase(device, next);
                        done = true;
                    });
                    if (done)
                    {
                        applied++;
                        progress = true;
                    }
                }
            }
            while (progress);
            if (applied > 0)
                await Run(s => s.RemoveDuplicateOccurrences());
            return applied;
        }

        // ---- Compaction ----

        /// <summary>Own files since the last base file after which a new one is written (about a month of daily syncs with changes).</summary>
        public const int CompactEvery = 50;

        /// <summary>
        /// When this device has written <see cref="CompactEvery"/> files since its last base file, writes a new base file: its full state,
        /// numbered like a change file. A device that is behind jumps to it, so every older file of this device can go
        /// (<see cref="PruneAsync"/>); this is what keeps a shared folder from growing a file per sync forever. Call it after
        /// <see cref="ExportAsync"/>. Returns whether a base file was written.
        /// </summary>
        public async Task<bool> CompactAsync(ISyncStore store, int every = CompactEvery)
        {
            await state.MaterializeAsync();
            var device = "";
            long seq = 0, baseSeq = 0;
            await Run(s => { device = s.Device; seq = s.PeerSeq(device); baseSeq = s.BaseSeq(device); });
            if (seq - baseSeq < every)
                return false;

            var batch = default(SyncBatch);
            await Run(s => batch = s.Base(seq + 1));
            await store.WriteAsync(device, batch!.Seq, Encode(Name(device, batch.Seq), batch));
            await Run(s => s.SetBase(device, batch.Seq));
            return true;
        }

        /// <summary>Deletes, in a store that can, the files that a base file already replaces (of every device whose base this device applied).</summary>
        public async Task<int> PruneAsync(ISyncStore store)
        {
            if (store is not IPrunableSyncStore prunable)
                return 0;
            var bases = new Dictionary<string, long>();
            await Run(s =>
            {
                foreach (var device in s.Devices())
                    bases[device.DeviceId] = device.BaseSeq;
            });

            var deleted = 0;
            foreach (var (device, baseSeq) in bases.Where(b => b.Value > 0))
            {
                // Highest first: a delete that fails half way leaves the lowest files, which a device behind still applies in order and then
                // reaches the hole before the base; deleting upwards could leave non-base files in front of it (a stuck reader)
                foreach (var seq in (await prunable.SeqsAsync(device)).Where(n => n < baseSeq).OrderByDescending(n => n))
                {
                    await prunable.DeleteAsync(device, seq);
                    deleted++;
                }
            }
            return deleted;
        }

        // ---- Snapshots (a new device, "replace my data") ----

        /// <summary>Everything this device has, with the last file of every device it contains.</summary>
        public async Task<byte[]> CreateSnapshotAsync()
        {
            await state.MaterializeAsync();
            SyncBatch? batch = null;
            await Run(s => batch = s.Snapshot());
            return Encode("snapshot", batch!);
        }

        /// <summary>
        /// Applies a snapshot. <paramref name="replaceLocalData"/> first empties every synced table (a new device, or "replace my
        /// data" when pairing); without it the snapshot is merged like any change file.
        /// </summary>
        public async Task ApplySnapshotAsync(byte[] data, bool replaceLocalData)
        {
            var batch = Decode("snapshot", data);
            if (!replaceLocalData)
                await state.MaterializeAsync(); // as in ApplyAsync: local edits get their clock before the merge
            await Run(s =>
            {
                s.CheckVersions(batch);
                if (!s.CanApply(batch))
                    throw new InvalidOperationException("The snapshot refers to rows it does not contain.");
                s.ApplySnapshot(batch, replaceLocalData);
            });
        }

        // ---- State, for tests and diagnostics ----

        /// <summary>Every synced row as one line (global id, clock, device, deleted, values with global ids): equal on devices in step.</summary>
        public async Task<string> DescribeAsync()
        {
            var lines = "";
            await Run(s => lines = s.Describe());
            return lines;
        }

        // ---- Plumbing ----

        private static string Name(string device, long seq) => $"{device}/{seq:D8}";

        private byte[] Encode(string name, SyncBatch batch) => SyncCrypto.Seal(key, name, JsonSerializer.SerializeToUtf8Bytes(batch));

        private SyncBatch Decode(string name, byte[] data) =>
            JsonSerializer.Deserialize<SyncBatch>(SyncCrypto.Open(key, name, data))!;

        private async Task<SyncState> StateRow()
        {
            var db = await database.GetConnectionAsync();
            return await db.FindAsync<SyncState>(1) ?? new SyncState();
        }

        private Task Run(Action<Session> work) =>
            database.RunInTransactionAsync(db =>
            {
                var row = db.Find<SyncState>(1);
                if (row is not { Enabled: true })
                    throw new InvalidOperationException("Sync is off.");
                var session = new Session(db, row, _time);
                work(session);
                session.Save();
            });

        /// <summary>The work of one database transaction.</summary>
        private sealed class Session(SQLiteConnection db, SyncState state, TimeProvider time)
        {
            private static readonly JsonElement NullValue = JsonSerializer.SerializeToElement<string?>(null);
            private readonly Dictionary<(string, int), string?> _gids = [];

            public string Device => state.DeviceId;

            public int SchemaVersion => db.ExecuteScalar<int>("PRAGMA user_version");

            public void Save() => db.Execute("UPDATE SyncState SET LastClock = ? WHERE Id = 1", state.LastClock);

            public long PeerSeq(string device) => db.Find<SyncPeerState>(device)?.LastSeq ?? 0;

            public void SetPeerSeq(string device, long seq) =>
                db.InsertOrReplace(new SyncPeerState { DeviceId = device, LastSeq = seq, BaseSeq = BaseSeq(device) });

            public List<SyncPeerState> Devices() => db.Table<SyncPeerState>().ToList();

            public long BaseSeq(string device) => db.Find<SyncPeerState>(device)?.BaseSeq ?? 0;

            public void SetBase(string device, long seq) =>
                db.InsertOrReplace(new SyncPeerState { DeviceId = device, LastSeq = Math.Max(PeerSeq(device), seq), BaseSeq = seq });

            private sealed class OccurrenceRow
            {
                public int LocalId { get; set; }
                public string OccurrenceKey { get; set; } = "";
                public string GlobalId { get; set; } = "";
            }

            /// <summary>
            /// Two devices that created the same occurrence of a schedule before they synced (see <see cref="Helpers.RecurringOccurrences"/>) have
            /// two transactions for it: the one with the lowest sync id stays. Every device picks the same one, and the others are deleted as a
            /// normal local edit, so the delete reaches the devices that have not done it themselves.
            /// </summary>
            public int RemoveDuplicateOccurrences()
            {
                var rows = db.Query<OccurrenceRow>(
                    "SELECT t.Id AS LocalId, t.OccurrenceKey AS OccurrenceKey, m.GlobalId AS GlobalId FROM \"Transaction\" t " +
                    "JOIN SyncMeta m ON m.TableName = 'Transaction' AND m.LocalId = t.Id " +
                    "WHERE t.OccurrenceKey IN (SELECT OccurrenceKey FROM \"Transaction\" WHERE OccurrenceKey IS NOT NULL GROUP BY OccurrenceKey HAVING COUNT(*) > 1)");
                var removed = 0;
                foreach (var group in rows.GroupBy(r => r.OccurrenceKey))
                {
                    // Raw deletes, not LedgerService.DeleteAsync: a transaction a schedule just made has no receipt; one the user added a receipt file
                    // to and then lost to the lowest id leaves that file on disk (the usual orphan, no data is lost)
                    foreach (var extra in group.OrderBy(r => r.GlobalId, StringComparer.Ordinal).Skip(1))
                    {
                        db.Execute("DELETE FROM \"TransactionTag\" WHERE TransactionId = ?", extra.LocalId);
                        db.Execute("DELETE FROM \"TransactionAttachment\" WHERE TransactionId = ?", extra.LocalId);
                        db.Execute("DELETE FROM \"Transaction\" WHERE Id = ?", extra.LocalId);
                        removed++;
                    }
                }
                return removed;
            }

            // ---- Reading rows ----

            private string RawSql(string table)
            {
                var mapping = db.GetMapping(SyncSchema.TypeOf(table));
                var pairs = string.Join(", ", mapping.Columns.Select(c => $"'{c.Name}', \"{c.Name}\""));
                return $"SELECT json_object({pairs}) FROM \"{table}\" WHERE \"{mapping.PK.Name}\" = ?";
            }

            /// <summary>The row as SQLite holds it (foreign keys are local IDs), without its key; null when there is no such row.</summary>
            private Dictionary<string, JsonElement>? RawFields(string table, int localId)
            {
                var json = db.ExecuteScalar<string?>(RawSql(table), localId);
                return json == null ? null : WithoutKey(table, JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!);
            }

            private Dictionary<string, JsonElement> WithoutKey(string table, Dictionary<string, JsonElement> fields)
            {
                fields.Remove(db.GetMapping(SyncSchema.TypeOf(table)).PK.Name);
                return fields;
            }

            private string? GidOf(string table, int localId)
            {
                if (!_gids.TryGetValue((table, localId), out var gid))
                    _gids[(table, localId)] = gid = db.ExecuteScalar<string?>(
                        "SELECT GlobalId FROM SyncMeta WHERE TableName = ? AND LocalId = ?", table, localId);
                return gid;
            }

            /// <summary>Local IDs of foreign keys to global ids, and without the columns that only mean something on this device (also used for a revived row's kept values, which therefore lose their local path: it is not on this device any more).</summary>
            private Dictionary<string, JsonElement> ToWire(string table, Dictionary<string, JsonElement> fields)
            {
                foreach (var (column, parent) in SyncSchema.ForeignKeys[table])
                {
                    if (fields.TryGetValue(column, out var value) && value.ValueKind == JsonValueKind.Number)
                        fields[column] = GidOf(parent, value.GetInt32()) is { } gid ? JsonSerializer.SerializeToElement(gid) : NullValue;
                }
                if (SyncSchema.LocalOnlyColumns.TryGetValue(table, out var local))
                {
                    foreach (var column in local)
                        fields.Remove(column);
                }
                return fields;
            }

            private SyncChange? ToChange(SyncMeta meta)
            {
                if (meta.Deleted)
                {
                    // The last values go along, so a device that gets the delete (or a snapshot) can bring the row back if it turns out to be in use
                    var kept = meta.Payload == null ? null : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(meta.Payload);
                    return new SyncChange(meta.TableName, meta.GlobalId, meta.Clock, meta.DeviceId, true, kept == null ? null : ToWire(meta.TableName, kept));
                }
                var fields = RawFields(meta.TableName, meta.LocalId);
                return fields == null
                    ? null // deleted after it was collected: the next collection sends the delete
                    : new SyncChange(meta.TableName, meta.GlobalId, meta.Clock, meta.DeviceId, false, ToWire(meta.TableName, fields));
            }

            public List<SyncChange> DirtyChanges() =>
                db.Query<SyncMeta>("SELECT * FROM SyncMeta WHERE Dirty = 1 ORDER BY Clock")
                    .Select(ToChange).OfType<SyncChange>().ToList();

            public void MarkExported(SyncBatch batch)
            {
                foreach (var change in batch.Changes)
                {
                    var meta = db.Find<SyncMeta>(change.GlobalId);
                    if (meta != null && meta.Clock == change.Clock && meta.DeviceId == change.DeviceId)
                    {
                        meta.Dirty = false;
                        db.Update(meta);
                    }
                }
                SetPeerSeq(batch.DeviceId, batch.Seq);
            }

            // ---- Applying ----

            public void CheckVersions(SyncBatch batch)
            {
                if (batch.Format > FormatVersion || batch.Schema > SchemaVersion)
                    throw new SyncVersionException("The other device runs a newer version of the app.");
            }

            /// <summary>True when every row a change refers to is known here, or comes with the batch.</summary>
            public bool CanApply(SyncBatch batch)
            {
                var inBatch = batch.Changes.Where(c => !c.Deleted).Select(c => c.GlobalId).ToHashSet();
                foreach (var change in batch.Changes.Where(c => !c.Deleted))
                {
                    foreach (var column in SyncSchema.ForeignKeys[change.Table].Keys)
                    {
                        if (!change.Fields!.TryGetValue(column, out var value) || value.ValueKind != JsonValueKind.String)
                            continue;
                        var gid = value.GetString()!;
                        if (inBatch.Contains(gid))
                            continue;
                        // A deleted row with no kept values (its delete arrived without its creation) cannot be brought back either
                        var known = db.Find<SyncMeta>(gid);
                        if (known == null || known is { Deleted: true, Payload: null })
                            return false;
                    }
                }
                return true;
            }

            public void ApplyBatch(SyncBatch batch) => WithApplying(() => ApplyChanges(batch));

            /// <summary>The writes of <paramref name="work"/> are other devices' changes, not local edits: the triggers stay quiet.</summary>
            private void WithApplying(Action work)
            {
                db.Execute("UPDATE SyncState SET Applying = 1 WHERE Id = 1");
                try
                {
                    work();
                }
                finally
                {
                    db.Execute("UPDATE SyncState SET Applying = 0 WHERE Id = 1");
                }
            }

            private void ApplyChanges(SyncBatch batch)
            {
                var byTable = batch.Changes.ToLookup(c => c.Table);
                foreach (var table in SyncSchema.Order)
                {
                    foreach (var change in byTable[table].Where(c => !c.Deleted))
                        ApplyUpsert(change);
                }
                foreach (var table in SyncSchema.Order.Reverse())
                {
                    foreach (var change in byTable[table].Where(c => c.Deleted))
                        ApplyDelete(change);
                }
            }

            private void ApplyUpsert(SyncChange change)
            {
                var meta = db.Find<SyncMeta>(change.GlobalId);
                if (meta != null && SyncClock.Compare(change.Clock, change.DeviceId, meta.Clock, meta.DeviceId) <= 0)
                    return;
                SyncClock.Observe(state, change.Clock);

                var localId = WriteRow(change.Table, change.Fields!, meta?.LocalId ?? 0);
                db.InsertOrReplace(new SyncMeta
                {
                    GlobalId = change.GlobalId,
                    TableName = change.Table,
                    LocalId = localId,
                    Clock = change.Clock,
                    DeviceId = change.DeviceId
                });
            }

            private void ApplyDelete(SyncChange change)
            {
                var meta = db.Find<SyncMeta>(change.GlobalId);
                if (meta != null && SyncClock.Compare(change.Clock, change.DeviceId, meta.Clock, meta.DeviceId) <= 0)
                    return;
                SyncClock.Observe(state, change.Clock);

                if (meta == null)
                {
                    db.Insert(new SyncMeta
                    {
                        GlobalId = change.GlobalId, TableName = change.Table, Clock = change.Clock, DeviceId = change.DeviceId,
                        Deleted = true, Payload = KeptValues(change)
                    });
                    return;
                }
                if (meta.Deleted)
                {
                    meta.Clock = change.Clock;
                    meta.DeviceId = change.DeviceId;
                    meta.Payload ??= KeptValues(change);
                    db.Update(meta);
                    return;
                }
                if (IsUsed(meta.TableName, meta.LocalId))
                {
                    // This device still uses the row: the delete is refused by writing the row again, newer than the delete
                    Touch(meta);
                    db.Update(meta);
                    return;
                }

                // Raw deletes: the children's SyncMeta stays live until their own tombstones arrive (the device that deleted the parent sends them), then
                // they converge. Their receipt files are not touched here; the blob stage owns files.
                foreach (var (child, parent) in SyncSchema.CascadeChildren.Where(c => c.Value == meta.TableName))
                {
                    foreach (var (childTable, column) in SyncSchema.ReferencesTo(parent).Where(r => r.Table == child))
                        db.Execute($"DELETE FROM \"{childTable}\" WHERE \"{column}\" = ?", meta.LocalId);
                }
                meta.Payload = RawJson(meta.TableName, meta.LocalId);
                db.Execute($"DELETE FROM \"{meta.TableName}\" WHERE \"{PkOf(meta.TableName)}\" = ?", meta.LocalId);
                meta.Deleted = true;
                meta.Dirty = false;
                meta.Clock = change.Clock;
                meta.DeviceId = change.DeviceId;
                db.Update(meta);
            }

            /// <summary>The values a delete carried, kept as they came (foreign keys as global id strings: the rows they name may not exist here, and <see cref="ToWire"/> leaves strings alone).</summary>
            private static string? KeptValues(SyncChange change) =>
                change.Fields == null ? null : JsonSerializer.Serialize(change.Fields);

            private string? RawJson(string table, int localId) =>
                RawFields(table, localId) is { } fields ? JsonSerializer.Serialize(fields) : null;

            private string PkOf(string table) => db.GetMapping(SyncSchema.TypeOf(table)).PK.Name;

            private bool IsUsed(string table, int localId) =>
                SyncSchema.ReferencesTo(table)
                    .Where(r => !SyncSchema.CascadeChildren.TryGetValue(r.Table, out var parent) || parent != table)
                    .Any(r => db.ExecuteScalar<int>($"SELECT COUNT(*) FROM \"{r.Table}\" WHERE \"{r.Column}\" = ?", localId) > 0);

            private void Touch(SyncMeta meta)
            {
                meta.Clock = SyncClock.Tick(state, time);
                meta.DeviceId = state.DeviceId;
                meta.Dirty = true;
            }

            /// <summary>Inserts or updates a row from wire values (keeping <paramref name="hintLocalId"/> as its ID when it had one); returns its local ID.</summary>
            private int WriteRow(string table, Dictionary<string, JsonElement> fields, int hintLocalId)
            {
                var mapping = db.GetMapping(SyncSchema.TypeOf(table));
                var pk = mapping.PK.Name;
                var columns = new List<string>();
                var values = new List<object?>();
                foreach (var column in mapping.Columns.Where(c => c.Name != pk))
                {
                    if (!fields.TryGetValue(column.Name, out var value))
                        continue; // a column an older version did not send keeps its value
                    object? sql;
                    if (SyncSchema.ForeignKeys[table].TryGetValue(column.Name, out var parent))
                    {
                        object? local = value.ValueKind == JsonValueKind.String ? EnsureLive(value.GetString()!) : null;
                        sql = local ?? (column.IsNullable ? null : 0);
                    }
                    else
                    {
                        sql = ToSql(value);
                    }
                    columns.Add(column.Name);
                    values.Add(sql);
                }

                var exists = hintLocalId > 0 && db.ExecuteScalar<int>($"SELECT COUNT(*) FROM \"{table}\" WHERE \"{pk}\" = ?", hintLocalId) > 0;
                if (exists)
                {
                    values.Add(hintLocalId);
                    db.Execute($"UPDATE \"{table}\" SET {string.Join(", ", columns.Select(c => $"\"{c}\" = ?"))} WHERE \"{pk}\" = ?", [.. values]);
                    return hintLocalId;
                }

                // A column of this device that is not text-or-null (the model says an empty string): a new row starts with the empty value, not NULL
                if (SyncSchema.LocalOnlyColumns.TryGetValue(table, out var localOnly))
                {
                    foreach (var name in localOnly.Where(n => !columns.Contains(n) && IsNonNullString(mapping.Columns.First(c => c.Name == n))))
                    {
                        columns.Add(name);
                        values.Add("");
                    }
                }

                if (hintLocalId > 0)
                {
                    columns.Add(pk); // a row that comes back keeps its old ID, so what refers to it still finds it
                    values.Add(hintLocalId);
                }
                db.Execute($"INSERT INTO \"{table}\" ({string.Join(", ", columns.Select(c => $"\"{c}\""))}) VALUES ({string.Join(", ", columns.Select(_ => "?"))})", [.. values]);
                return hintLocalId > 0 ? hintLocalId : db.ExecuteScalar<int>("SELECT last_insert_rowid()");
            }

            private static readonly NullabilityInfoContext Nullability = new();

            private static bool IsNonNullString(TableMapping.Column column) =>
                column.ColumnType == typeof(string) && Nullability.Create(column.PropertyInfo).WriteState == NullabilityState.NotNull;

            private static object? ToSql(JsonElement value) => value.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.Number => value.TryGetInt64(out var whole) ? (object)whole : value.GetDouble(), // a plain ternary would turn the whole number into a double
                JsonValueKind.True => 1L,
                JsonValueKind.False => 0L,
                _ => value.GetString()
            };

            /// <summary>The local ID of a row a change refers to; a row deleted here is brought back from its kept values (and sent again).</summary>
            private int EnsureLive(string gid)
            {
                var meta = db.Find<SyncMeta>(gid) ?? throw new InvalidOperationException("CanApply must have checked that this row is known.");
                if (!meta.Deleted)
                    return meta.LocalId;

                var fields = ToWire(meta.TableName, WithoutKey(meta.TableName, JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(meta.Payload!)!));
                meta.LocalId = WriteRow(meta.TableName, fields, meta.LocalId);
                meta.Deleted = false;
                meta.Payload = null;
                Touch(meta);
                db.Update(meta);
                return meta.LocalId;
            }

            // ---- Snapshots ----

            /// <summary>The full state as a numbered base file of this device: it replaces all this device's older files.</summary>
            public SyncBatch Base(long seq) =>
                Snapshot() with { Seq = seq, PeerSeqs = null, IsBase = true };

            public SyncBatch Snapshot()
            {
                var changes = db.Query<SyncMeta>("SELECT * FROM SyncMeta").Select(ToChange).OfType<SyncChange>().ToList();
                var peers = db.Table<SyncPeerState>().ToDictionary(p => p.DeviceId, p => p.LastSeq);
                return new SyncBatch(FormatVersion, SchemaVersion, state.DeviceId, 0, changes, peers);
            }

            public void ApplySnapshot(SyncBatch snapshot, bool replaceLocalData) => WithApplying(() =>
            {
                if (replaceLocalData)
                {
                    foreach (var table in SyncSchema.Order.Reverse())
                        db.Execute($"DELETE FROM \"{table}\"");
                    db.DeleteAll<SyncMeta>();
                    db.DeleteAll<SyncDirty>();
                    // What this device had applied from other devices belonged to the data that is gone: the snapshot says where to go on
                    db.Execute("DELETE FROM SyncPeerState WHERE DeviceId <> ?", state.DeviceId);
                    _gids.Clear();
                }

                ApplyChanges(snapshot);
                foreach (var (device, seq) in snapshot.PeerSeqs ?? [])
                {
                    if (seq > PeerSeq(device))
                        SetPeerSeq(device, seq);
                }
            });

            // ---- Description ----

            public string Describe()
            {
                var metas = db.Query<SyncMeta>("SELECT * FROM SyncMeta").ToList();
                var lines = new List<string>();
                foreach (var table in SyncSchema.Order)
                {
                    foreach (var meta in metas.Where(m => m.TableName == table).OrderBy(m => m.GlobalId, StringComparer.Ordinal))
                    {
                        var values = "-";
                        if (!meta.Deleted && RawFields(table, meta.LocalId) is { } fields)
                            values = string.Join(",", ToWire(table, fields).OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => $"{f.Key}={f.Value}"));
                        lines.Add($"{table}|{meta.GlobalId}|{meta.Clock}|{meta.DeviceId}|{meta.Deleted}|{values}");
                    }
                }
                return string.Join('\n', lines);
            }
        }
    }
}
