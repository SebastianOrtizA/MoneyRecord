using System.Diagnostics;
using System.Security.Cryptography;
using AwesomeAssertions;
using MoneyRecord.Models;
using Xunit;
using Xunit.Abstractions;

namespace MoneyRecord.Tests.Sync;

/// <summary>Stage 26.0 spike: does a folder of encrypted per-device change files converge? Findings are in the Stage 26.0 pull request.</summary>
public sealed class SyncPrototypeTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"moneyrecord-sync-{Guid.NewGuid():N}");
    private readonly byte[] _key = SyncCrypto.NewKey();
    private readonly ITestOutputHelper _output;
    private readonly List<ProtoDevice> _devices = [];
    private long _now = 1_800_000_000_000;

    public SyncPrototypeTests(ITestOutputHelper output)
    {
        _output = output;
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        foreach (var device in _devices)
            device.Dispose();
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { }
    }

    private ProtoDevice NewDevice(string id, long skewMs = 0)
    {
        var device = new ProtoDevice(id, Path.Combine(_folder, $"{id}.db3"), _key, () => _now + skewMs);
        _devices.Add(device);
        return device;
    }

    private DirTransport Transport => new(Path.Combine(_folder, "shared"));

    private void Sync(ProtoDevice from, ProtoDevice to)
    {
        from.Export(Transport);
        to.Import(Transport);
    }

    private void SyncAll()
    {
        foreach (var d in _devices) d.Export(Transport);
        foreach (var d in _devices) d.Import(Transport);
    }

    [Fact]
    public void NewRowsReachTheOtherDevice_WithForeignKeysMapped()
    {
        var a = NewDevice("A");
        var b = NewDevice("B");
        var account = a.AddAccount("Cash");
        var category = a.AddCategory("Food");
        // B already has rows of its own, so the local IDs differ between the devices
        b.AddAccount("Savings");
        a.AddTransaction(category, account, 1250, "lunch");

        Sync(a, b);

        b.LiveCount(ProtoDevice.TransactionT).Should().Be(1);
        b.OrphanCount().Should().Be(0);
        b.LiveGids(ProtoDevice.AccountT).Should().Contain(account);
    }

    [Fact]
    public void ConcurrentEdits_LastWriterWins_OnEveryDevice()
    {
        var a = NewDevice("A");
        var b = NewDevice("B");
        var category = a.AddCategory("Food");
        Sync(a, b);

        a.EditCategory(category, "Groceries");
        _now += 1000;
        b.EditCategory(category, "Restaurants"); // later
        SyncAll();
        SyncAll();

        a.Dump().Should().Be(b.Dump());
        a.Dump().Should().Contain("name=Restaurants");
    }

    [Fact]
    public void EditAfterADelete_BringsTheRowBack_DeleteAfterAnEdit_Wins()
    {
        var a = NewDevice("A");
        var b = NewDevice("B");
        var account = a.AddAccount("Cash");
        var category = a.AddCategory("Food");
        var keep = a.AddTransaction(category, account, 100);
        var drop = a.AddTransaction(category, account, 200);
        SyncAll();
        SyncAll();

        a.DeleteTransaction(keep);
        _now += 1000;
        b.EditTransaction(keep, 111); // edit newer than the delete: the row stays
        b.EditTransaction(drop, 222);
        _now += 1000;
        a.DeleteTransaction(drop); // delete newer than the edit: the row goes
        SyncAll();
        SyncAll();

        a.Dump().Should().Be(b.Dump());
        a.LiveGids(ProtoDevice.TransactionT).Should().ContainSingle().Which.Should().Be(keep);
    }

    [Fact]
    public void CategoryDeletedWhileTheOtherDeviceUsesIt_IsKeptEverywhere()
    {
        var a = NewDevice("A");
        var b = NewDevice("B");
        var account = a.AddAccount("Cash");
        var category = a.AddCategory("Food");
        SyncAll();
        SyncAll();

        a.DeleteCategory(category).Should().BeTrue();
        _now += 1000;
        var t = b.AddTransaction(category, account, 500); // B has not seen the delete
        SyncAll();
        SyncAll();
        SyncAll();

        a.Dump().Should().Be(b.Dump());
        a.OrphanCount().Should().Be(0);
        b.OrphanCount().Should().Be(0);
        a.LiveGids(ProtoDevice.CategoryT).Should().Contain(category);

        // B deleting that one transaction afterwards must not leave the devices disagreeing about the category
        b.DeleteTransaction(t);
        SyncAll();
        SyncAll();
        a.Dump().Should().Be(b.Dump());
    }

    [Fact]
    public void ImportingTwice_ChangesNothing()
    {
        var a = NewDevice("A");
        var b = NewDevice("B");
        a.AddTransaction(a.AddCategory("Food"), a.AddAccount("Cash"), 1);
        a.Export(Transport);

        b.Import(Transport).Should().Be(1);
        var before = b.Dump();
        b.Import(Transport).Should().Be(0);
        b.Dump().Should().Be(before);
    }

    [Fact]
    public void ExportThatStoppedAfterWritingItsFile_IsNotRepeatedWithOtherContent()
    {
        var a = NewDevice("A");
        var b = NewDevice("B");
        a.AddCategory("Food");
        a.AfterWrite = () => throw new IOException("killed");
        var act = () => a.Export(Transport);
        act.Should().Throw<IOException>();
        a.DirtyCount.Should().Be(1); // the device doesn't know its file was written

        b.Import(Transport); // B applies file 1
        a.AfterWrite = null;
        a.AddCategory("Travel");
        a.Export(Transport); // must be file 2, not a different file 1
        b.Import(Transport);
        SyncAll();

        a.Dump().Should().Be(b.Dump());
        b.LiveCount(ProtoDevice.CategoryT).Should().Be(2);
    }

    [Fact]
    public void FileWithAMissingPredecessor_WaitsInsteadOfApplyingOutOfOrder()
    {
        var a = NewDevice("A");
        var b = NewDevice("B");
        var c = NewDevice("C");
        var category = a.AddCategory("Food");
        var account = a.AddAccount("Cash");
        Sync(a, b);
        b.AddTransaction(category, account, 100); // refers to A's rows
        b.Export(Transport);

        // C sees only B's file: its rows are not there yet, so nothing is applied (and nothing breaks)
        var folder = Path.Combine(_folder, "partial");
        File.Copy(Path.Combine(_folder, "shared", "B", "00000001.chg"), Path.Combine(Directory.CreateDirectory(Path.Combine(folder, "B")).FullName, "00000001.chg"));
        c.Import(new DirTransport(folder)).Should().Be(0);
        c.OrphanCount().Should().Be(0);

        // Once A's file is there, both apply, whatever the order they were listed in
        c.Import(Transport).Should().Be(2);
        c.OrphanCount().Should().Be(0);
        c.LiveCount(ProtoDevice.TransactionT).Should().Be(1);
    }

    [Fact]
    public void BatchesAreEncryptedAndTiedToTheirPath()
    {
        var a = NewDevice("A");
        a.AddCategory("Secret Category Name");
        a.Export(Transport);
        var path = Path.Combine(_folder, "shared", "A", "00000001.chg");
        var bytes = File.ReadAllBytes(path);

        System.Text.Encoding.UTF8.GetString(bytes).Should().NotContain("Secret");
        SyncCrypto.Open(_key, "A/00000001.chg", bytes).Should().NotBeEmpty();
        ((Action)(() => SyncCrypto.Open(_key, "A/00000002.chg", bytes))).Should().Throw<CryptographicException>();
        ((Action)(() => SyncCrypto.Open(SyncCrypto.NewKey(), "A/00000001.chg", bytes))).Should().Throw<CryptographicException>();
        bytes[40] ^= 1;
        ((Action)(() => SyncCrypto.Open(_key, "A/00000001.chg", bytes))).Should().Throw<CryptographicException>();
    }

    [Fact]
    public void NewDevice_StartsFromASnapshot()
    {
        var a = NewDevice("A");
        var b = NewDevice("B");
        var account = a.AddAccount("Cash");
        var category = a.AddCategory("Food");
        var t = a.AddTransaction(category, account, 100);
        a.AddTransaction(category, account, 200);
        a.DeleteTransaction(t);
        a.EditCategory(category, "Groceries");
        a.Export(Transport);

        var fresh = NewDevice("D");
        fresh.ApplySnapshot(a.Snapshot());

        fresh.Dump().Should().Be(a.Dump());
    }

    /// <summary>The convergence criterion of the spike: seeded random operations on three devices, syncing at random moments.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(18)]
    [InlineData(19)]
    [InlineData(20)]
    public void RandomOperationsOnThreeDevices_ConvergeToTheSameState(int seed)
    {
        var rng = new Random(seed);
        var devices = new[] { NewDevice("A"), NewDevice("B", skewMs: 300_000 /* a phone 5 minutes fast */), NewDevice("C", skewMs: -60_000) };

        for (var step = 0; step < 200; step++)
        {
            _now += rng.Next(1, 5000);
            var d = devices[rng.Next(devices.Length)];
            var accounts = d.LiveGids(ProtoDevice.AccountT);
            var categories = d.LiveGids(ProtoDevice.CategoryT);
            var transactions = d.LiveGids(ProtoDevice.TransactionT);
            string Pick(IReadOnlyList<string> list) => list[rng.Next(list.Count)];

            switch (rng.Next(100))
            {
                case < 8: d.AddAccount($"Account {step}"); break;
                case < 18: d.AddCategory($"Category {step}"); break;
                case < 50 when accounts.Count > 0 && categories.Count > 0:
                    d.AddTransaction(Pick(categories), Pick(accounts), rng.Next(1, 100000), $"t{step}"); break;
                case < 65 when transactions.Count > 0: d.EditTransaction(Pick(transactions), rng.Next(1, 100000)); break;
                case < 72 when categories.Count > 0: d.EditCategory(Pick(categories), $"Renamed {step}"); break;
                case < 82 when transactions.Count > 0: d.DeleteTransaction(Pick(transactions)); break;
                case < 87 when categories.Count > 0: d.DeleteCategory(Pick(categories)); break;
                case < 90 when accounts.Count > 0: d.DeleteAccount(Pick(accounts)); break;
                case >= 90:
                    d.Export(Transport);
                    if (rng.Next(3) > 0)
                        devices[rng.Next(devices.Length)].Import(Transport); // sometimes a device stays behind
                    break;
            }
        }

        // Everyone comes back online, in a different order each round
        for (var round = 0; round < 6; round++)
        {
            foreach (var d in devices.OrderBy(_ => rng.Next())) d.Export(Transport);
            foreach (var d in devices.OrderBy(_ => rng.Next())) d.Import(Transport);
        }

        devices.Sum(d => d.DirtyCount).Should().Be(0, "nothing is left to send");
        devices[1].Dump().Should().Be(devices[0].Dump());
        devices[2].Dump().Should().Be(devices[0].Dump());
        devices.Sum(d => d.OrphanCount()).Should().Be(0);
    }

    /// <summary>Numbers for the findings (size and time of a year of use on one device); no assertion beyond sanity.</summary>
    [Fact]
    public void YearOfUse_Sizes()
    {
        var a = NewDevice("A");
        var account = a.AddAccount("Cash");
        var categories = Enumerable.Range(0, 12).Select(i => a.AddCategory($"Category {i}")).ToList();
        var rng = new Random(7);
        var transactions = new List<string>();

        var files = 0;
        var exportWatch = Stopwatch.StartNew();
        for (var day = 0; day < 365; day++)
        {
            for (var slot = 0; slot < 4; slot++) // four syncs a day
            {
                _now += 6 * 3600 * 1000;
                if (rng.Next(100) < 55) transactions.Add(a.AddTransaction(categories[rng.Next(categories.Count)], account, rng.Next(100, 20000), $"Shop {day}"));
                if (rng.Next(100) < 10 && transactions.Count > 0) a.EditTransaction(transactions[rng.Next(transactions.Count)], rng.Next(100, 20000));
                if (a.Export(Transport) > 0) files++;
            }
        }
        exportWatch.Stop();

        var dir = Path.Combine(_folder, "shared", "A");
        var bytes = Directory.GetFiles(dir).Sum(f => new FileInfo(f).Length);
        var snapshot = a.Snapshot();

        var b = NewDevice("B");
        var replay = Stopwatch.StartNew();
        b.Import(Transport);
        replay.Stop();
        var c = NewDevice("C");
        var snap = Stopwatch.StartNew();
        c.ApplySnapshot(snapshot);
        snap.Stop();

        _output.WriteLine($"transactions: {transactions.Count}");
        _output.WriteLine($"change files: {files}, total {bytes / 1024.0:F0} KiB, average {bytes / (double)files:F0} B");
        _output.WriteLine($"snapshot: {snapshot.Length / 1024.0:F0} KiB");
        _output.WriteLine($"export of the year: {exportWatch.ElapsedMilliseconds} ms; new device replaying {files} files: {replay.ElapsedMilliseconds} ms; from the snapshot: {snap.ElapsedMilliseconds} ms");

        b.Dump().Should().Be(a.Dump());
        c.Dump().Should().Be(a.Dump());
    }
}
