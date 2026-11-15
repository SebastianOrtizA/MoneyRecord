using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;
using SQLite;

namespace MoneyRecord.Services
{
    public sealed class SyncStateService(DatabaseInitializer database, TimeProvider? time = null) : ISyncStateService
    {
        private readonly TimeProvider _time = time ?? TimeProvider.System;

        public async Task<bool> IsEnabledAsync() => (await StateAsync()).Enabled;

        public async Task<string> GetDeviceIdAsync() => (await StateAsync()).DeviceId;

        private async Task<SyncState> StateAsync()
        {
            var db = await database.GetConnectionAsync();
            return await db.FindAsync<SyncState>(1) ?? new SyncState();
        }

        public async Task EnableAsync()
        {
            await database.RunInTransactionAsync(db =>
            {
                var state = db.Find<SyncState>(1) ?? new SyncState();
                if (state.Enabled)
                    return;

                state.Enabled = true;
                state.DeviceId = Guid.NewGuid().ToString("N");
                state.TriggerHash = SyncCapture.Install(db);
                db.InsertOrReplace(state);

                foreach (var type in SyncCapture.SyncedTables)
                {
                    var mapping = db.GetMapping(type);
                    db.Execute($"INSERT INTO SyncDirty (TableName, LocalId, Deleted) SELECT '{mapping.TableName}', \"{mapping.PK.Name}\", 0 FROM \"{mapping.TableName}\"");
                }
            });
        }

        public async Task DisableAsync() =>
            await database.RunInTransactionAsync(db => SyncCapture.ResetOnRestore(db));

        public async Task<int> MaterializeAsync()
        {
            var marked = 0;
            await database.RunInTransactionAsync(db =>
            {
                var state = db.Find<SyncState>(1);
                if (state is not { Enabled: true })
                    return;

                var dirty = db.Query<SyncDirty>("SELECT * FROM SyncDirty ORDER BY Id");
                if (dirty.Count == 0)
                    return;

                var tables = SyncCapture.SyncedTables.Select(t => db.GetMapping(t)).ToDictionary(m => m.TableName);
                foreach (var group in dirty.GroupBy(d => (d.TableName, d.LocalId)).OrderBy(g => g.Max(d => d.Id)))
                {
                    var (table, localId) = group.Key;
                    var last = group.MaxBy(d => d.Id)!;
                    var exists = db.ExecuteScalar<int>($"SELECT COUNT(*) FROM \"{table}\" WHERE \"{tables[table].PK.Name}\" = ?", localId) > 0;
                    var meta = db.Query<SyncMeta>("SELECT * FROM SyncMeta WHERE TableName = ? AND LocalId = ?", table, localId).FirstOrDefault();

                    if (exists)
                    {
                        meta ??= new SyncMeta { GlobalId = Guid.NewGuid().ToString("N"), TableName = table, LocalId = localId };
                        meta.Deleted = false;
                        meta.Payload = null;
                    }
                    else if (meta == null)
                    {
                        continue; // added and deleted before anything was sent: no other device ever knew it
                    }
                    else
                    {
                        meta.Deleted = true;
                        meta.Payload = group.Where(d => d.Deleted).MaxBy(d => d.Id)?.Payload ?? meta.Payload;
                    }

                    meta.Clock = Tick(state);
                    meta.DeviceId = state.DeviceId;
                    meta.Dirty = true;
                    db.InsertOrReplace(meta);
                    marked++;
                }

                db.Execute("DELETE FROM SyncDirty WHERE Id <= ?", dirty[^1].Id);
                db.Update(state);
            });
            return marked;
        }

        /// <summary>The next hybrid logical clock value: wall clock milliseconds in the high bits, a counter for the same millisecond.</summary>
        private long Tick(SyncState state)
        {
            var physical = Math.Max(_time.GetUtcNow().ToUnixTimeMilliseconds(), state.LastClock >> 16);
            state.LastClock = physical == state.LastClock >> 16 ? state.LastClock + 1 : physical << 16;
            return state.LastClock;
        }
    }
}
