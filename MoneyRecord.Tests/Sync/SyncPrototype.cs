using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MoneyRecord.Models;
using SQLite;

namespace MoneyRecord.Tests.Sync;

// Stage 26.0 research spike. THROWAWAY: this is a model of the sync design to learn from, not code the app uses.
// Findings: in the Stage 26.0 pull request. Nothing here is referenced from the app projects.

/// <summary>One synced row's sync data, in a side table (the live tables are untouched).</summary>
internal sealed class SyncMeta
{
    [PrimaryKey]
    public string GlobalId { get; set; } = "";
    public string TableName { get; set; } = "";
    public int LocalId { get; set; }
    public long Clock { get; set; }
    public string DeviceId { get; set; } = "";
    public bool Deleted { get; set; }
    /// <summary>Written locally and not exported yet.</summary>
    public bool Dirty { get; set; }
    /// <summary>The last fields of a deleted row (kept so a delete that turns out wrong can be undone).</summary>
    public string? Payload { get; set; }
}

internal sealed class SyncPeer
{
    [PrimaryKey]
    public string DeviceId { get; set; } = "";
    public long LastSeq { get; set; }
}

internal sealed record SyncChange(string Table, string GlobalId, long Clock, string DeviceId, bool Deleted, Dictionary<string, string?>? Fields);

internal sealed record SyncBatch(int Format, int Schema, string DeviceId, long Seq, List<SyncChange> Changes);

internal interface IProtoTransport
{
    void Write(string path, byte[] data);
    byte[]? Read(string path);
    IReadOnlyList<string> Folders();
}

/// <summary>A plain folder, as a desktop sync client (or a SAF tree) would show it.</summary>
internal sealed class DirTransport(string root) : IProtoTransport
{
    public void Write(string path, byte[] data)
    {
        var full = Path.Combine(root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temp = full + ".tmp";
        File.WriteAllBytes(temp, data);
        File.Move(temp, full, overwrite: true);
    }

    public byte[]? Read(string path)
    {
        var full = Path.Combine(root, path);
        return File.Exists(full) ? File.ReadAllBytes(full) : null;
    }

    public IReadOnlyList<string> Folders() =>
        Directory.Exists(root) ? Directory.GetDirectories(root).Select(d => Path.GetFileName(d)!).ToList() : [];
}

/// <summary>gzip then AES-256-GCM; the file path is authenticated so a file moved to another name fails.</summary>
internal static class SyncCrypto
{
    private const byte Version = 1;

    public static byte[] NewKey() => RandomNumberGenerator.GetBytes(32);

    public static byte[] Seal(byte[] key, string path, byte[] plain)
    {
        using var packed = new MemoryStream();
        using (var gz = new GZipStream(packed, CompressionLevel.Optimal, leaveOpen: true))
            gz.Write(plain);
        var data = packed.ToArray();

        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var cipher = new byte[data.Length];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, data, cipher, tag, Encoding.UTF8.GetBytes(path));

        var result = new byte[1 + 12 + 16 + cipher.Length];
        result[0] = Version;
        nonce.CopyTo(result, 1);
        tag.CopyTo(result, 13);
        cipher.CopyTo(result, 29);
        return result;
    }

    public static byte[] Open(byte[] key, string path, byte[] sealedData)
    {
        if (sealedData.Length < 29 || sealedData[0] != Version)
            throw new CryptographicException("Unknown format.");
        var nonce = sealedData.AsSpan(1, 12);
        var tag = sealedData.AsSpan(13, 16);
        var cipher = sealedData.AsSpan(29);
        var data = new byte[cipher.Length];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, cipher, tag, data, Encoding.UTF8.GetBytes(path));

        using var gz = new GZipStream(new MemoryStream(data), CompressionMode.Decompress);
        using var output = new MemoryStream();
        gz.CopyTo(output);
        return output.ToArray();
    }
}

/// <summary>
/// One device: a real SQLite file with the app's Account / Category / Transaction tables plus the sync side tables.
/// Writes stamp a hybrid logical clock; export writes the device's own change files; import applies the other devices'.
/// </summary>
internal sealed class ProtoDevice : IDisposable
{
    public const string AccountT = "Account", CategoryT = "Category", TransactionT = "Transaction";
    private const int FormatVersion = 1;
    private const int SchemaVersion = 1;

    private readonly SQLiteConnection _db;
    private readonly Func<long> _nowMs;
    private readonly byte[] _key;
    private long _last;
    private int _counter;

    /// <summary>Test hook: runs after a change file is written and before the device records it.</summary>
    public Action? AfterWrite { get; set; }

    public ProtoDevice(string deviceId, string path, byte[] key, Func<long> nowMs)
    {
        DeviceId = deviceId;
        _key = key;
        _nowMs = nowMs;
        _db = new SQLiteConnection(path);
        _db.CreateTable<Account>();
        _db.CreateTable<Category>();
        _db.CreateTable<Transaction>();
        _db.CreateTable<SyncMeta>();
        _db.CreateTable<SyncPeer>();
        _last = _db.ExecuteScalar<long>("SELECT IFNULL(MAX(Clock), 0) FROM SyncMeta");
    }

    public string DeviceId { get; }

    public void Dispose() => _db.Dispose();

    // ---- Clock ----

    private long Tick()
    {
        var physical = Math.Max(_nowMs(), _last >> 16);
        _last = physical == _last >> 16 ? _last + 1 : physical << 16;
        return _last;
    }

    private void Observe(long remote)
    {
        if (remote > _last)
            _last = remote;
    }

    private static int Compare(long clockA, string deviceA, long clockB, string deviceB) =>
        clockA != clockB ? clockA.CompareTo(clockB) : string.CompareOrdinal(deviceA, deviceB);

    // ---- Local writes (what a repository would do) ----

    public string AddAccount(string name, string currency = "EUR")
    {
        var row = new Account { Name = name, CurrencyCode = currency };
        _db.Insert(row);
        return Stamp(AccountT, row.Id, NewGid());
    }

    public string AddCategory(string name, CategoryType type = CategoryType.Expense)
    {
        var row = new Category { Name = name, Type = type };
        _db.Insert(row);
        return Stamp(CategoryT, row.Id, NewGid());
    }

    public string AddTransaction(string categoryGid, string accountGid, long cents, string description = "")
    {
        var row = new Transaction
        {
            Date = new DateTime(2026, 1, 1).AddDays(_counter % 365),
            Description = description,
            AmountCents = cents,
            Type = TransactionType.Expense,
            CategoryId = LiveLocalId(categoryGid),
            AccountId = LiveLocalId(accountGid)
        };
        _db.Insert(row);
        return Stamp(TransactionT, row.Id, NewGid());
    }

    public void EditTransaction(string gid, long cents)
    {
        var meta = LiveMeta(gid);
        var row = _db.Get<Transaction>(meta.LocalId);
        row.AmountCents = cents;
        _db.Update(row);
        Stamp(TransactionT, row.Id, gid);
    }

    public void EditCategory(string gid, string name)
    {
        var meta = LiveMeta(gid);
        var row = _db.Get<Category>(meta.LocalId);
        row.Name = name;
        _db.Update(row);
        Stamp(CategoryT, row.Id, gid);
    }

    public void DeleteTransaction(string gid) => DeleteRow(gid);

    /// <summary>Refused (false) while a transaction uses the category, like the app refuses account deletes with transfers.</summary>
    public bool DeleteCategory(string gid)
    {
        if (HasLiveChildren(LiveMeta(gid)))
            return false;
        DeleteRow(gid);
        return true;
    }

    public bool DeleteAccount(string gid)
    {
        if (HasLiveChildren(LiveMeta(gid)))
            return false;
        DeleteRow(gid);
        return true;
    }

    public IReadOnlyList<string> LiveGids(string table) =>
        _db.Table<SyncMeta>().Where(m => m.TableName == table && !m.Deleted).OrderBy(m => m.GlobalId).ToList().Select(m => m.GlobalId).ToList();

    private string NewGid() => $"{DeviceId}-{++_counter}";

    private string Stamp(string table, int localId, string gid, bool deleted = false, string? payload = null)
    {
        _db.InsertOrReplace(new SyncMeta
        {
            GlobalId = gid,
            TableName = table,
            LocalId = localId,
            Clock = Tick(),
            DeviceId = DeviceId,
            Deleted = deleted,
            Dirty = true,
            Payload = payload
        });
        return gid;
    }

    private SyncMeta LiveMeta(string gid)
    {
        var meta = _db.Find<SyncMeta>(gid);
        if (meta == null || meta.Deleted)
            throw new InvalidOperationException($"{gid} is not a live row here.");
        return meta;
    }

    private int LiveLocalId(string gid) => LiveMeta(gid).LocalId;

    private void DeleteRow(string gid)
    {
        var meta = LiveMeta(gid);
        var payload = JsonSerializer.Serialize(FieldsOf(meta));
        DeleteLocal(meta);
        Stamp(meta.TableName, meta.LocalId, gid, deleted: true, payload);
    }

    private void DeleteLocal(SyncMeta meta)
    {
        switch (meta.TableName)
        {
            case AccountT: _db.Delete<Account>(meta.LocalId); break;
            case CategoryT: _db.Delete<Category>(meta.LocalId); break;
            default: _db.Delete<Transaction>(meta.LocalId); break;
        }
    }

    private bool HasLiveChildren(SyncMeta parent) => parent.TableName switch
    {
        CategoryT => _db.Table<Transaction>().Any(t => t.CategoryId == parent.LocalId),
        AccountT => _db.Table<Transaction>().Any(t => t.AccountId == parent.LocalId),
        _ => false
    };

    // ---- Fields <-> rows (foreign keys travel as global ids) ----

    private Dictionary<string, string?> FieldsOf(SyncMeta meta)
    {
        switch (meta.TableName)
        {
            case AccountT:
                var a = _db.Get<Account>(meta.LocalId);
                return new() { ["name"] = a.Name, ["currency"] = a.CurrencyCode, ["initial"] = a.InitialBalanceCents.ToString() };
            case CategoryT:
                var c = _db.Get<Category>(meta.LocalId);
                return new() { ["name"] = c.Name, ["type"] = ((int)c.Type).ToString(), ["icon"] = c.IconCode };
            default:
                var t = _db.Get<Transaction>(meta.LocalId);
                return new()
                {
                    ["date"] = t.Date.Ticks.ToString(),
                    ["description"] = t.Description,
                    ["cents"] = t.AmountCents.ToString(),
                    ["type"] = ((int)t.Type).ToString(),
                    ["category"] = GidOf(CategoryT, t.CategoryId),
                    ["account"] = t.AccountId is { } id ? GidOf(AccountT, id) : null
                };
        }
    }

    private string GidOf(string table, int localId) =>
        _db.Table<SyncMeta>().First(m => m.TableName == table && m.LocalId == localId && !m.Deleted).GlobalId;

    /// <summary>Inserts or updates the live row of <paramref name="gid"/> from <paramref name="fields"/>; returns its local ID.</summary>
    private int WriteRow(string table, string gid, Dictionary<string, string?> fields)
    {
        var existing = _db.Find<SyncMeta>(gid);
        var localId = existing is { Deleted: false } ? existing.LocalId : 0;
        switch (table)
        {
            case AccountT:
                var a = localId != 0 ? _db.Get<Account>(localId) : new Account();
                a.Name = fields["name"]!;
                a.CurrencyCode = fields["currency"]!;
                a.InitialBalanceCents = long.Parse(fields["initial"]!);
                Save(a);
                return a.Id;
            case CategoryT:
                var c = localId != 0 ? _db.Get<Category>(localId) : new Category();
                c.Name = fields["name"]!;
                c.Type = (CategoryType)int.Parse(fields["type"]!);
                c.IconCode = fields["icon"]!;
                Save(c);
                return c.Id;
            default:
                var t = localId != 0 ? _db.Get<Transaction>(localId) : new Transaction();
                t.Date = new DateTime(long.Parse(fields["date"]!));
                t.Description = fields["description"]!;
                t.AmountCents = long.Parse(fields["cents"]!);
                t.Type = (TransactionType)int.Parse(fields["type"]!);
                t.CategoryId = EnsureLive(fields["category"]!);
                t.AccountId = fields["account"] is { } acc ? EnsureLive(acc) : null;
                Save(t);
                return t.Id;
        }

        void Save<T>(T row) where T : IEntity
        {
            if (row.Id != 0) _db.Update(row); else _db.Insert(row);
        }
    }

    /// <summary>The local ID of a parent row; a parent deleted here but still used by an arriving row is brought back (and exported).</summary>
    private int EnsureLive(string gid)
    {
        var meta = _db.Find<SyncMeta>(gid)!;
        if (!meta.Deleted)
            return meta.LocalId;

        var fields = JsonSerializer.Deserialize<Dictionary<string, string?>>(meta.Payload!)!;
        var localId = WriteRow(meta.TableName, gid, fields);
        Stamp(meta.TableName, localId, gid);
        return localId;
    }

    // ---- Export ----

    private SyncPeer Peer(string id) => _db.Find<SyncPeer>(id) ?? new SyncPeer { DeviceId = id };

    private string PathOf(string device, long seq) => $"{device}/{seq:D8}.chg";

    /// <summary>Writes the rows changed here since the last export as the next change file; returns the number of changes.</summary>
    public int Export(IProtoTransport transport)
    {
        var mine = Peer(DeviceId);

        // A previous export that wrote its file and stopped before recording it: take that file as exported
        while (transport.Read(PathOf(DeviceId, mine.LastSeq + 1)) is { } leftover)
        {
            var old = Decode(PathOf(DeviceId, mine.LastSeq + 1), leftover);
            _db.RunInTransaction(() =>
            {
                foreach (var change in old.Changes)
                {
                    var meta = _db.Find<SyncMeta>(change.GlobalId);
                    if (meta != null && meta.Clock == change.Clock && meta.DeviceId == change.DeviceId)
                    {
                        meta.Dirty = false;
                        _db.Update(meta);
                    }
                }
                mine.LastSeq = old.Seq;
                _db.InsertOrReplace(mine);
            });
        }

        var dirty = _db.Table<SyncMeta>().Where(m => m.Dirty).ToList();
        if (dirty.Count == 0)
            return 0;

        var batch = new SyncBatch(FormatVersion, SchemaVersion, DeviceId, mine.LastSeq + 1, dirty.Select(ToChange).ToList());
        transport.Write(PathOf(DeviceId, batch.Seq), Encode(PathOf(DeviceId, batch.Seq), batch));
        AfterWrite?.Invoke();

        _db.RunInTransaction(() =>
        {
            foreach (var meta in dirty)
            {
                // Rewritten since the batch was read: stays dirty (not possible single-threaded, kept for the real thing)
                meta.Dirty = false;
                _db.Update(meta);
            }
            mine.LastSeq = batch.Seq;
            _db.InsertOrReplace(mine);
        });
        return dirty.Count;
    }

    private SyncChange ToChange(SyncMeta m) =>
        new(m.TableName, m.GlobalId, m.Clock, m.DeviceId, m.Deleted, m.Deleted ? null : FieldsOf(m));

    /// <summary>Everything this device has, as one batch (for a new device and for compaction).</summary>
    public byte[] Snapshot() =>
        Encode("snapshot", new SyncBatch(FormatVersion, SchemaVersion, DeviceId, 0,
            _db.Table<SyncMeta>().ToList().Select(ToChange).ToList()));

    public void ApplySnapshot(byte[] data)
    {
        var batch = Decode("snapshot", data);
        if (!CanApply(batch))
            throw new InvalidOperationException("Snapshot refers to rows it does not contain.");
        _db.RunInTransaction(() => ApplyChanges(batch));
    }

    private byte[] Encode(string path, SyncBatch batch) =>
        SyncCrypto.Seal(_key, path, JsonSerializer.SerializeToUtf8Bytes(batch));

    private SyncBatch Decode(string path, byte[] data) =>
        JsonSerializer.Deserialize<SyncBatch>(SyncCrypto.Open(_key, path, data))!;

    // ---- Import ----

    /// <summary>Applies the next change file of every other device, as far as they can be applied; returns the number of files applied.</summary>
    public int Import(IProtoTransport transport)
    {
        var applied = 0;
        bool progress;
        do
        {
            progress = false;
            foreach (var peerId in transport.Folders().Where(f => f != DeviceId).Order(StringComparer.Ordinal))
            {
                var peer = Peer(peerId);
                var path = PathOf(peerId, peer.LastSeq + 1);
                if (transport.Read(path) is not { } data)
                    continue;

                var batch = Decode(path, data);
                if (batch.Schema > SchemaVersion)
                    throw new InvalidOperationException("Update the app on the other device.");
                if (!CanApply(batch))
                    continue; // waits for a row another device has not delivered yet

                _db.RunInTransaction(() =>
                {
                    ApplyChanges(batch);
                    peer.LastSeq = batch.Seq;
                    _db.InsertOrReplace(peer); // with the rows: a stop in between leaves neither
                });
                applied++;
                progress = true;
            }
        }
        while (progress);
        return applied;
    }

    /// <summary>True when every row a transaction in the batch refers to is known here or comes with the batch.</summary>
    private bool CanApply(SyncBatch batch)
    {
        var inBatch = batch.Changes.Where(c => !c.Deleted).Select(c => c.GlobalId).ToHashSet();
        foreach (var change in batch.Changes.Where(c => !c.Deleted && c.Table == TransactionT))
        {
            foreach (var key in new[] { "category", "account" })
            {
                // A deleted row with no kept fields (its delete arrived without its creation) can't be brought back either
                if (change.Fields![key] is not { } gid || inBatch.Contains(gid))
                    continue;
                var known = _db.Find<SyncMeta>(gid);
                if (known == null || known is { Deleted: true, Payload: null })
                    return false;
            }
        }
        return true;
    }

    private static int Order(string table) => table switch { AccountT => 0, CategoryT => 1, _ => 2 };

    private void ApplyChanges(SyncBatch batch)
    {
        foreach (var change in batch.Changes.Where(c => !c.Deleted).OrderBy(c => Order(c.Table)))
            ApplyUpsert(change);
        foreach (var change in batch.Changes.Where(c => c.Deleted).OrderByDescending(c => Order(c.Table)))
            ApplyDelete(change);
    }

    private void ApplyUpsert(SyncChange change)
    {
        var meta = _db.Find<SyncMeta>(change.GlobalId);
        if (meta != null && Compare(change.Clock, change.DeviceId, meta.Clock, meta.DeviceId) <= 0)
            return;
        Observe(change.Clock);

        var localId = WriteRow(change.Table, change.GlobalId, change.Fields!);
        _db.InsertOrReplace(new SyncMeta
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
        var meta = _db.Find<SyncMeta>(change.GlobalId);
        if (meta != null && Compare(change.Clock, change.DeviceId, meta.Clock, meta.DeviceId) <= 0)
            return;
        Observe(change.Clock);

        if (meta == null)
        {
            _db.Insert(new SyncMeta { GlobalId = change.GlobalId, TableName = change.Table, Clock = change.Clock, DeviceId = change.DeviceId, Deleted = true });
            return;
        }
        if (meta.Deleted)
        {
            meta.Clock = change.Clock;
            meta.DeviceId = change.DeviceId;
            _db.Update(meta);
            return;
        }
        if (HasLiveChildren(meta))
        {
            // This device still uses the row: the delete is refused by writing the row again, newer than the delete
            Stamp(meta.TableName, meta.LocalId, meta.GlobalId);
            return;
        }

        var payload = JsonSerializer.Serialize(FieldsOf(meta));
        DeleteLocal(meta);
        meta.Deleted = true;
        meta.Dirty = false;
        meta.Clock = change.Clock;
        meta.DeviceId = change.DeviceId;
        meta.Payload = payload;
        _db.Update(meta);
    }

    // ---- State, for comparing devices ----

    /// <summary>The live rows by global id (no local IDs) and every row's clock: equal on devices that have converged.</summary>
    public string Dump()
    {
        var metas = _db.Table<SyncMeta>().ToList();
        var lines = new List<string>();
        foreach (var m in metas.OrderBy(m => m.GlobalId, StringComparer.Ordinal))
        {
            var fields = m.Deleted ? "-" : string.Join(",", FieldsOf(m).OrderBy(f => f.Key).Select(f => $"{f.Key}={f.Value}"));
            lines.Add($"{m.TableName}|{m.GlobalId}|{m.Clock}|{m.DeviceId}|{m.Deleted}|{fields}");
        }
        return string.Join('\n', lines);
    }

    /// <summary>Live transactions whose category or account is not a live row (must never happen).</summary>
    public int OrphanCount() =>
        _db.ExecuteScalar<int>("SELECT COUNT(*) FROM \"Transaction\" WHERE CategoryId NOT IN (SELECT Id FROM Category) OR (AccountId IS NOT NULL AND AccountId NOT IN (SELECT Id FROM Account))");

    public int LiveCount(string table) => _db.Table<SyncMeta>().Count(m => m.TableName == table && !m.Deleted);

    public int DirtyCount => _db.Table<SyncMeta>().Count(m => m.Dirty);
}
