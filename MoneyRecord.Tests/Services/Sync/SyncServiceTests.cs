using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Repositories;
using MoneyRecord.Services.Sync;
using SQLite;
using Xunit;

namespace MoneyRecord.Tests.Services.Sync;

/// <summary>Stage 26.3a-2: the app-level sync flow (turn on, pair by code, sync now) between a "PC" and a "phone", over loopback.</summary>
public sealed class SyncServiceTests : IAsyncLifetime
{
    private static readonly string[] Loopback = ["127.0.0.1"];
    private readonly List<Device> _devices = [];

    private sealed class MemorySecrets(bool supported = true) : ISyncSecretStore
    {
        private string? _json;
        public bool IsSupported => supported;
        public Task<string?> GetAsync() => Task.FromResult(_json);
        public bool Fail { get; set; }
        public Task SetAsync(string? json)
        {
            if (Fail && json != null)
                throw new IOException("the keystore is not available");
            _json = json;
            return Task.CompletedTask;
        }
    }

    private sealed class Platform(bool canListen, string name) : ISyncPlatform
    {
        public bool CanListen => canListen;
        public string DeviceName => name;
    }

    private sealed class FakeFolder(string? path = null, ISyncStore? store = null) : ISyncFolder
    {
        public string? Path { get; set; } = path;
        public bool IsSupported => true;
        public string? Name => Path == null ? null : System.IO.Path.GetFileName(Path);
        public Task<bool> PickAsync() => Task.FromResult(true);
        public void Clear() => Path = null;
        public bool ThrowOnOpen { get; set; }
        public ISyncStore? Open() => ThrowOnOpen ? throw new UnauthorizedAccessException("permission taken back")
            : store ?? (Path == null ? null : new DirectorySyncStore(Path));
    }

    private sealed class BrokenStore : ISyncStore
    {
        public Task<IReadOnlyList<string>> DevicesAsync() => throw new IOException("the provider is offline");
        public Task<long> LastSeqAsync(string deviceId) => throw new IOException("the provider is offline");
        public Task<byte[]?> ReadAsync(string deviceId, long seq) => throw new IOException("the provider is offline");
        public Task WriteAsync(string deviceId, long seq, byte[] data) => throw new IOException("the provider is offline");
    }

    private sealed class Device
    {
        public required string Folder { get; init; }
        public required SQLiteAsyncConnection Db { get; init; }
        public required SyncService Service { get; init; }
        public required DatabaseInitializer Database { get; init; }
        public required AppPaths Paths { get; init; }
        public required SyncStateService State { get; init; }
        public required MemorySecrets Secrets { get; init; }
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var device in _devices)
        {
            await device.Service.DisableAsync();
            await device.Db.CloseAsync();
            try { Directory.Delete(device.Folder, recursive: true); } catch (IOException) { }
        }
    }

    private async Task<Device> NewDeviceAsync(bool canListen, string name, Func<string, Task<(string Address, int Port)?>>? find = null, ISyncFolder? shared = null)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"moneyrecord-svc-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var paths = new AppPaths(folder);
        var database = new DatabaseInitializer(paths);
        var db = await database.GetConnectionAsync();
        var state = new SyncStateService(database);
        var secrets = new MemorySecrets();
        var service = new SyncService(database, state, secrets, new Platform(canListen, name), paths,
            new AccountRepository(database), new CategoryRepository(database), localAddresses: () => Loopback, findComputer: find, sharedFolder: shared);
        var device = new Device { Folder = folder, Db = db, Database = database, Paths = paths, Service = service, State = state, Secrets = secrets };
        _devices.Add(device);
        return device;
    }

    private static async Task<List<string>> NamesAsync(Device d, string table) =>
        await d.Db.QueryScalarsAsync<string>($"SELECT Name FROM \"{table}\" ORDER BY Name");

    [Fact]
    public async Task TurningOnAtTheComputer_StartsListening_AndAPhoneCannotTurnItOnThatWay()
    {
        var pc = await NewDeviceAsync(canListen: true, "My PC");
        var phone = await NewDeviceAsync(canListen: false, "My Phone");

        await pc.Service.EnableAsync();

        var status = await pc.Service.GetStatusAsync();
        status.Should().BeEquivalentTo(new { Enabled = true, Listening = true, CanListen = true, DeviceName = "My PC" });
        var act = () => phone.Service.EnableAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task APhone_PairsByCode_AndTakesTheComputersData()
    {
        var pc = await NewDeviceAsync(true, "My PC");
        var phone = await NewDeviceAsync(false, "My Phone");
        await pc.Db.InsertAsync(new Category { Name = "Test From PC", Type = CategoryType.Expense });
        await phone.Db.InsertAsync(new Category { Name = "Test Only On Phone", Type = CategoryType.Expense });
        await pc.Service.EnableAsync();

        var code = await pc.Service.CreatePairingCodeAsync();
        var outcome = await phone.Service.PairAsync(code.Text);

        outcome.Should().Be(SyncOutcome.Done);
        (await NamesAsync(phone, "Category")).Should().Contain("Test From PC").And.NotContain("Test Only On Phone");
        var status = await phone.Service.GetStatusAsync();
        status.Enabled.Should().BeTrue();
        status.Peers.Should().ContainSingle().Which.Name.Should().Be("My PC");
    }

    [Fact]
    public async Task AcceptedCodes_AreOneTime_AndAFailedPairingLeavesSyncOff()
    {
        var pc = await NewDeviceAsync(true, "My PC");
        var phone = await NewDeviceAsync(false, "My Phone");
        var other = await NewDeviceAsync(false, "Other Phone");
        await pc.Service.EnableAsync();
        var code = await pc.Service.CreatePairingCodeAsync();
        await phone.Service.PairAsync(code.Text);

        (await other.Service.PairAsync(code.Text)).Should().Be(SyncOutcome.NotRecognised);
        (await other.Service.PairAsync("not a code")).Should().Be(SyncOutcome.BadCode);

        (await other.Service.GetStatusAsync()).Enabled.Should().BeFalse();
        (await other.State.IsEnabledAsync()).Should().BeFalse("a pairing that failed does not leave change capture running");
    }

    [Fact]
    public async Task SyncNow_MovesChangesBothWays_AndTheComputerAppliesWhatArrives()
    {
        var pc = await NewDeviceAsync(true, "My PC");
        var phone = await NewDeviceAsync(false, "My Phone");
        await pc.Service.EnableAsync();
        await phone.Service.PairAsync((await pc.Service.CreatePairingCodeAsync()).Text);

        var arrived = new TaskCompletionSource();
        pc.Service.Changed += () => arrived.TrySetResult();
        await pc.Db.InsertAsync(new Payee { Name = "Test Payee From PC" });
        await phone.Db.InsertAsync(new Category { Name = "Test Category From Phone", Type = CategoryType.Expense });

        await pc.Service.SyncNowAsync(); // the computer sends what it has (nobody to connect to: it listens)
        (await phone.Service.SyncNowAsync()).Should().Be(SyncOutcome.Done);
        await arrived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        (await NamesAsync(phone, "Payee")).Should().Contain("Test Payee From PC");
        (await NamesAsync(pc, "Category")).Should().Contain("Test Category From Phone");
        (await phone.Service.GetStatusAsync()).Peers.Single().LastSync.Should().NotBeNull();
    }

    [Fact]
    public async Task TheComputer_ListsThePhoneThatPaired_AndRemovingItLocksItOut()
    {
        var pc = await NewDeviceAsync(true, "My PC");
        var phone = await NewDeviceAsync(false, "My Phone");
        await pc.Service.EnableAsync();
        await phone.Service.PairAsync((await pc.Service.CreatePairingCodeAsync()).Text);
        await phone.Service.SyncNowAsync(); // signs in once

        var status = await pc.Service.GetStatusAsync();
        var listed = status.Peers.Should().ContainSingle().Which;
        listed.Name.Should().Be("My Phone");
        listed.LastSync.Should().NotBeNull("the computer saw it sign in");

        await pc.Service.RemovePeerAsync(listed.DeviceId);

        (await phone.Service.SyncNowAsync()).Should().Be(SyncOutcome.NotRecognised);
        (await pc.Service.GetStatusAsync()).Peers.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenTheComputerMovedAddress_ThePhoneFindsItByBeacon_AndRemembersTheNewAddress()
    {
        var pc = await NewDeviceAsync(true, "My PC");
        var phone = await NewDeviceAsync(false, "My Phone");
        await pc.Service.EnableAsync();
        await phone.Service.PairAsync((await pc.Service.CreatePairingCodeAsync()).Text);
        var peer = (await phone.Service.GetStatusAsync()).Peers.Single();

        // The phone's saved address is stale (another port stands in for another address); a new service reads the saved profile
        var json = await phone.Secrets.GetAsync();
        await phone.Secrets.SetAsync(json!.Replace($"\"Port\":{peer.Port}", "\"Port\":1"));
        var asked = new List<string>();
        var again = new SyncService(phone.Database, new SyncStateService(phone.Database), phone.Secrets, new Platform(false, "My Phone"), phone.Paths,
            new AccountRepository(phone.Database), new CategoryRepository(phone.Database),
            findComputer: id => { asked.Add(id); return Task.FromResult<(string, int)?>(("127.0.0.1", peer.Port)); });

        (await again.SyncNowAsync()).Should().Be(SyncOutcome.Done);

        asked.Should().Equal(peer.DeviceId);
        (await again.GetStatusAsync()).Peers.Single().Port.Should().Be(peer.Port);
    }

    [Fact]
    public async Task ThroughASharedFolder_TheDevicesSyncWithoutTheComputerAnswering()
    {
        var shared = Path.Combine(Path.GetTempPath(), $"moneyrecord-shared-{Guid.NewGuid():N}");
        try
        {
            var pc = await NewDeviceAsync(true, "My PC", shared: new FakeFolder(shared));
            var phone = await NewDeviceAsync(false, "My Phone", shared: new FakeFolder(shared));
            await pc.Service.EnableAsync();
            await phone.Service.PairAsync((await pc.Service.CreatePairingCodeAsync()).Text);
            var peer = (await phone.Service.GetStatusAsync()).Peers.Single();

            // Away from home: the phone's saved port no longer answers and nobody answers the beacon
            await phone.Secrets.SetAsync((await phone.Secrets.GetAsync())!.Replace($"\"Port\":{peer.Port}", "\"Port\":1"));
            var away = new SyncService(phone.Database, new SyncStateService(phone.Database), phone.Secrets, new Platform(false, "My Phone"), phone.Paths,
                new AccountRepository(phone.Database), new CategoryRepository(phone.Database),
                findComputer: _ => Task.FromResult<(string, int)?>(null), sharedFolder: new FakeFolder(shared));
            await phone.Db.InsertAsync(new Category { Name = "Test Category Away", Type = CategoryType.Expense });

            (await away.SyncNowAsync()).Should().Be(SyncOutcome.Done, "the folder did the work");
            await pc.Service.SyncNowAsync(); // later, the computer reads the folder

            (await NamesAsync(pc, "Category")).Should().Contain("Test Category Away");
        }
        finally
        {
            try { Directory.Delete(shared, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task AFolderThatCannotBeReached_IsReported_AndTheRestOfTheSyncStillRuns()
    {
        var pc = await NewDeviceAsync(true, "My PC", shared: new FakeFolder(store: new BrokenStore()));
        await pc.Service.EnableAsync();
        await pc.Db.InsertAsync(new Payee { Name = "Test Payee Local" });

        (await pc.Service.SyncNowAsync()).Should().Be(SyncOutcome.FolderUnavailable);

        // The local export still happened: the change file is in the device's own store
        var store = new DirectorySyncStore(pc.Paths.SyncFolder);
        (await store.DevicesAsync()).Should().NotBeEmpty();
    }

    [Fact]
    public async Task WhenTheComputerIsAwayAndTheFolderFails_TheFolderIsWhatTheUserHearsAbout()
    {
        var pc = await NewDeviceAsync(true, "My PC");
        var phone = await NewDeviceAsync(false, "My Phone");
        await pc.Service.EnableAsync();
        await phone.Service.PairAsync((await pc.Service.CreatePairingCodeAsync()).Text);
        var peer = (await phone.Service.GetStatusAsync()).Peers.Single();
        await phone.Secrets.SetAsync((await phone.Secrets.GetAsync())!.Replace($"\"Port\":{peer.Port}", "\"Port\":1"));
        var away = new SyncService(phone.Database, new SyncStateService(phone.Database), phone.Secrets, new Platform(false, "My Phone"), phone.Paths,
            new AccountRepository(phone.Database), new CategoryRepository(phone.Database),
            findComputer: _ => Task.FromResult<(string, int)?>(null), sharedFolder: new FakeFolder(store: new BrokenStore()));

        (await away.SyncNowAsync()).Should().Be(SyncOutcome.FolderUnavailable);
    }

    [Fact]
    public async Task AFolderWhoseOpenThrows_IsReportedToo()
    {
        var pc = await NewDeviceAsync(true, "My PC", shared: new FakeFolder { ThrowOnOpen = true });
        await pc.Service.EnableAsync();

        (await pc.Service.SyncNowAsync()).Should().Be(SyncOutcome.FolderUnavailable);
    }

    [Fact]
    public async Task TheFolder_IsOnlyForgottenByTheUser_OrWhenSyncIsTurnedOff()
    {
        var folder = new FakeFolder(Path.Combine(Path.GetTempPath(), "x-shared"));
        var pc = await NewDeviceAsync(true, "My PC", shared: folder);
        await pc.Service.EnableAsync();

        (await pc.Service.GetStatusAsync()).Should().BeEquivalentTo(new { FolderSupported = true, FolderName = "x-shared" });
        await pc.Service.DisableAsync();

        folder.Path.Should().BeNull();
        (await pc.Service.GetStatusAsync()).FolderName.Should().BeNull();
    }

    [Fact]
    public async Task APhone_CanForgetTheComputer()
    {
        var pc = await NewDeviceAsync(true, "My PC");
        var phone = await NewDeviceAsync(false, "My Phone");
        await pc.Service.EnableAsync();
        await phone.Service.PairAsync((await pc.Service.CreatePairingCodeAsync()).Text);

        await phone.Service.RemovePeerAsync((await phone.Service.GetStatusAsync()).Peers.Single().DeviceId);

        (await phone.Service.GetStatusAsync()).Peers.Should().BeEmpty();
    }

    [Fact]
    public async Task IfSecureStorageFails_PairingReplacesNothing_AndLeavesSyncOff()
    {
        var pc = await NewDeviceAsync(true, "My PC");
        var phone = await NewDeviceAsync(false, "My Phone");
        await phone.Db.InsertAsync(new Category { Name = "Test Mine", Type = CategoryType.Expense });
        await pc.Service.EnableAsync();
        phone.Secrets.Fail = true;

        var code = (await pc.Service.CreatePairingCodeAsync()).Text;
        var act = () => phone.Service.PairAsync(code);

        await act.Should().ThrowAsync<IOException>();
        (await NamesAsync(phone, "Category")).Should().Contain("Test Mine");
        (await phone.State.IsEnabledAsync()).Should().BeFalse();
        (await phone.Service.GetStatusAsync()).Enabled.Should().BeFalse();
    }
    [Fact]
    public async Task AComputerThatIsNotListening_IsReportedAsUnreachable()
    {
        var pc = await NewDeviceAsync(true, "My PC");
        var phone = await NewDeviceAsync(false, "My Phone");
        await pc.Service.EnableAsync();
        await phone.Service.PairAsync((await pc.Service.CreatePairingCodeAsync()).Text);
        await pc.Service.DisableAsync(); // the app on the computer is closed or sync is off there

        (await phone.Service.SyncNowAsync()).Should().Be(SyncOutcome.Unreachable);
    }

    [Fact]
    public async Task TurningOff_ForgetsTheKeyAndDevices_KeepsTheData()
    {
        var pc = await NewDeviceAsync(true, "My PC");
        await pc.Db.InsertAsync(new Category { Name = "Test Keep", Type = CategoryType.Expense });
        await pc.Service.EnableAsync();

        await pc.Service.DisableAsync();

        (await pc.Service.GetStatusAsync()).Should().BeEquivalentTo(new { Enabled = false, Listening = false });
        pc.Secrets.GetAsync().Result.Should().BeNull();
        (await pc.State.IsEnabledAsync()).Should().BeFalse();
        (await NamesAsync(pc, "Category")).Should().Contain("Test Keep");
    }

    [Fact]
    public async Task AfterARestart_TheComputerListensAgain_WithTheSameKey()
    {
        var pc = await NewDeviceAsync(true, "My PC");
        await pc.Service.EnableAsync();
        var key = JsonKey(pc.Secrets);

        var paths = new AppPaths(pc.Folder);
        var database = new DatabaseInitializer(paths);
        var restarted = new SyncService(database, new SyncStateService(database), pc.Secrets, new Platform(true, "My PC"), paths,
            new AccountRepository(database), new CategoryRepository(database), localAddresses: () => Loopback);

        (await restarted.GetStatusAsync()).Listening.Should().BeFalse("nothing listens until the app resumes");
        await restarted.ResumeAsync();
        (await restarted.GetStatusAsync()).Listening.Should().BeTrue();
        JsonKey(pc.Secrets).Should().Be(key);
        (await restarted.CreatePairingCodeAsync()).Text.Should().StartWith("moneyrecord-sync:1:");
    }

    private static string JsonKey(MemorySecrets secrets) =>
        System.Text.Json.JsonSerializer.Deserialize<SyncProfile>(secrets.GetAsync().Result!)!.Key;
}
