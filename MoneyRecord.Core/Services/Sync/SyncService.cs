using System.Security.Cryptography;
using System.Text.Json;
using MoneyRecord.Helpers;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;
using MoneyRecord.Services.Sync.Lan;

namespace MoneyRecord.Services.Sync
{
    public enum SyncOutcome
    {
        Done,

        /// <summary>The text is not a pairing code.</summary>
        BadCode,

        /// <summary>No device answered (off, another network, a firewall).</summary>
        Unreachable,

        /// <summary>The other device does not know this one (removed there), or the code expired or was used.</summary>
        NotRecognised,

        /// <summary>Something answered, but it is not the device in the code.</summary>
        WrongDevice,

        /// <summary>One of the two apps is older than the files the other writes.</summary>
        VersionMismatch,

        /// <summary>The shared folder could not be read or written (removed, permission taken back, a provider that is offline).</summary>
        FolderUnavailable
    }

    /// <param name="FolderSupported">This device can use a shared folder.</param>
    /// <param name="FolderName">The chosen shared folder; null when none is chosen (or it can no longer be reached).</param>
    public sealed record SyncStatus(bool Supported, bool Enabled, bool CanListen, bool Listening, string DeviceName, IReadOnlyList<SyncPeer> Peers,
        bool FolderSupported = false, string? FolderName = null);

    public sealed record PairingCode(string Text, DateTimeOffset Expires);

    public interface ISyncService
    {
        Task<SyncStatus> GetStatusAsync();

        /// <summary>Turns sync on at a device that listens (the desktop app): makes the key and the certificate, and starts listening.</summary>
        Task EnableAsync();

        /// <summary>At app start: listens again when sync is on and this device listens.</summary>
        Task ResumeAsync();

        /// <summary>A one-time code (also drawn as a QR) for another device to pair with this one.</summary>
        Task<PairingCode> CreatePairingCodeAsync();

        /// <summary>Pairs with the device that showed <paramref name="code"/> and takes its data in place of this device's own.</summary>
        Task<SyncOutcome> PairAsync(string code);

        Task<SyncOutcome> SyncNowAsync();

        /// <summary>
        /// Takes a device off the list. At the listening side it can no longer sign in (it is locked out at once); at the other side
        /// this device stops syncing with it. The shared key stays: to cut a device off from the files it already has, turn sync off and on.
        /// </summary>
        Task RemovePeerAsync(string deviceId);

        /// <summary>Lets the user pick the shared folder (needs sync on); false when cancelled.</summary>
        Task<bool> PickFolderAsync();

        /// <summary>Stops using the shared folder (its files stay where they are).</summary>
        Task ClearFolderAsync();

        /// <summary>Stops syncing and forgets the key and the devices (the data stays).</summary>
        Task DisableAsync();

        /// <summary>Changes from another device were applied while the app was open.</summary>
        event Action? Changed;
    }

    public sealed class SyncService(
        DatabaseInitializer database,
        SyncStateService syncState,
        ISyncSecretStore secrets,
        ISyncPlatform platform,
        IAppPaths paths,
        IAccountRepository accounts,
        ICategoryRepository categories,
        TimeProvider? time = null,
        Func<IReadOnlyList<string>>? localAddresses = null,
        Func<string, Task<(string Address, int Port)?>>? findComputer = null,
        ISyncBackground? background = null,
        ISyncFolder? sharedFolder = null) : ISyncService
    {
        private const int PreferredPort = 48731;
        private const int MaxNameLength = 64;
        private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);

        private readonly TimeProvider _time = time ?? TimeProvider.System;

        /// <summary>One long operation at a time: turning on, pairing, syncing, applying, turning off.</summary>
        private readonly SemaphoreSlim _busy = new(1, 1);

        /// <summary>Short changes to the profile (the list of devices); never held across the network.</summary>
        private readonly SemaphoreSlim _profileGate = new(1, 1);

        private SyncProfile? _profile;
        private LanSyncServer? _server;
        private LanBeacon? _beacon;

        public event Action? Changed;

        private DirectorySyncStore Store => new(paths.SyncFolder);

        // ---- Profile ----

        private async Task<SyncProfile?> LoadAsync()
        {
            if (_profile != null)
                return _profile;
            var json = await secrets.GetAsync();
            return _profile = json == null ? null : JsonSerializer.Deserialize<SyncProfile>(json);
        }

        private async Task SaveAsync(SyncProfile? profile)
        {
            await secrets.SetAsync(profile == null ? null : JsonSerializer.Serialize(profile));
            _profile = profile;
        }

        /// <summary>Changes the saved profile under the gate (the list of devices is touched by sign-ins as well as by the user).</summary>
        private async Task UpdateProfileAsync(Func<SyncProfile, bool> change)
        {
            await _profileGate.WaitAsync();
            try
            {
                if (await LoadAsync() is { } profile && change(profile))
                    await SaveAsync(profile);
            }
            finally
            {
                _profileGate.Release();
            }
        }

        private SyncEngine EngineOf(SyncProfile profile) =>
            new(database, syncState, Convert.FromBase64String(profile.Key), _time);

        private void InvalidateCaches()
        {
            accounts.InvalidateCache();
            categories.InvalidateCache();
        }

        private async Task<string?> SafeFolderNameAsync()
        {
            try
            {
                // Off the caller's thread: a cloud provider can take seconds to answer
                return sharedFolder == null ? null : await Task.Run(() => sharedFolder.Name);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return null; // a folder that can no longer be reached is shown as none chosen
            }
        }

        public async Task<SyncStatus> GetStatusAsync()
        {
            var profile = await LoadAsync();
            IReadOnlyList<SyncPeer> peers = profile == null ? []
                : profile.Certificate != null
                    ? [.. profile.Devices.Select(d => new SyncPeer(d.DeviceId, d.Name, [], 0, "", d.LastSeen))]
                    : [.. profile.Peers];
            return new SyncStatus(secrets.IsSupported, profile != null, platform.CanListen, _server != null,
                profile?.DeviceName ?? platform.DeviceName, peers, sharedFolder?.IsSupported ?? false, profile == null ? null : await SafeFolderNameAsync());
        }

        // ---- The devices that paired with this one (the listening side) ----

        private sealed class Registry(SyncService service) : ILanDeviceRegistry
        {
            public async Task<byte[]?> SecretOfAsync(string deviceId) =>
                await service.LoadAsync() is { } profile && profile.Devices.FirstOrDefault(d => d.DeviceId == deviceId) is { } device
                    ? Convert.FromBase64String(device.AuthSecret)
                    : null;

            public async Task<byte[]> RegisterAsync(string deviceId, string name)
            {
                var secret = RandomNumberGenerator.GetBytes(32);
                name = name.Trim();
                name = name.Length == 0 ? "Device" : name[..Math.Min(name.Length, MaxNameLength)];
                await service.UpdateProfileAsync(profile =>
                {
                    profile.Devices.RemoveAll(d => d.DeviceId == deviceId); // paired again: the old secret is gone
                    profile.Devices.Add(new SyncDevice(deviceId, name, Convert.ToBase64String(secret), service._time.GetUtcNow()));
                    return true;
                });
                return secret;
            }

            public Task SeenAsync(string deviceId) =>
                service.UpdateProfileAsync(profile =>
                {
                    var index = profile.Devices.FindIndex(d => d.DeviceId == deviceId);
                    if (index < 0)
                        return false;
                    profile.Devices[index] = profile.Devices[index] with { LastSeen = service._time.GetUtcNow() };
                    return true;
                });
        }

        // ---- Turning on ----

        public async Task EnableAsync()
        {
            if (!secrets.IsSupported || !platform.CanListen)
                throw new InvalidOperationException("This device cannot listen for others.");

            await _busy.WaitAsync();
            try
            {
                if (await LoadAsync() != null)
                    return;

                var (_, pfx) = LanCertificate.Create();
                var profile = new SyncProfile
                {
                    Key = Convert.ToBase64String(SyncCrypto.NewKey()),
                    Certificate = Convert.ToBase64String(pfx),
                    DeviceName = platform.DeviceName
                };
                await syncState.EnableAsync();
                await SaveAsync(profile);
                await StartListeningAsync(profile);
            }
            finally
            {
                _busy.Release();
            }
        }

        public async Task ResumeAsync()
        {
            if (_server == null && platform.CanListen && await LoadAsync() is { Certificate: not null } profile && await syncState.IsEnabledAsync())
                await StartListeningAsync(profile);
            // A phone that is paired syncs in the background; if sync is off nothing is scheduled
            if (!platform.CanListen && await LoadAsync() != null && await syncState.IsEnabledAsync())
                background?.Schedule();
        }

        private async Task StartListeningAsync(SyncProfile profile)
        {
            if (_server != null || profile.Certificate == null)
                return;
            var engine = EngineOf(profile);
            var server = new LanSyncServer(LanCertificate.Load(Convert.FromBase64String(profile.Certificate)), Convert.FromBase64String(profile.Key),
                await syncState.GetDeviceIdAsync(), profile.DeviceName, Store, engine.CreateSnapshotAsync, new Registry(this), _time, Store);
            server.StoreChanged += () => _ = ApplyIncomingAsync();
            try
            {
                server.Start(PreferredPort);
            }
            catch (System.Net.Sockets.SocketException)
            {
                server.Start(); // the usual port is taken: any free one (a paired phone then needs a new code)
            }
            _server = server;
            // Answers the phone's "where are you?" after this computer's address changed (best-effort)
            var beacon = new LanBeacon(await syncState.GetDeviceIdAsync(), () => server.Port);
            if (beacon.Start())
                _beacon = beacon;
            else
                await beacon.DisposeAsync();
        }

        // ---- Pairing ----

        public async Task<PairingCode> CreatePairingCodeAsync()
        {
            if (_server == null)
                await ResumeAsync();
            var server = _server ?? throw new InvalidOperationException("Sync is not listening.");
            var offer = server.CreatePairingOffer((localAddresses ?? LanAddresses.Local)(), CodeLifetime);
            return new PairingCode(offer.ToText(), DateTimeOffset.FromUnixTimeSeconds(offer.ExpiresUnix));
        }

        public async Task<SyncOutcome> PairAsync(string code)
        {
            if (LanPairingInfo.TryParse(code?.Trim()) is not { } info)
                return SyncOutcome.BadCode;
            if (!secrets.IsSupported || await LoadAsync() != null)
                throw new InvalidOperationException("Sync is already on.");

            await _busy.WaitAsync();
            try
            {
                await syncState.EnableAsync();
                try
                {
                    var deviceId = await syncState.GetDeviceIdAsync();
                    var (session, key, authSecret) = await LanSyncSession.PairAsync(info, deviceId, platform.DeviceName);
                    byte[] snapshot;
                    await using (session)
                        snapshot = await session.SnapshotAsync();

                    var profile = new SyncProfile
                    {
                        Key = Convert.ToBase64String(key),
                        AuthSecret = Convert.ToBase64String(authSecret),
                        DeviceName = platform.DeviceName,
                        Peers = [new SyncPeer(info.DeviceId, info.DeviceName, info.Addresses, info.Port, info.Fingerprint, _time.GetUtcNow())]
                    };
                    // The profile first: if secure storage fails nothing has been replaced. The data swap is one transaction, so if
                    // it fails nothing is lost either, and the profile is taken back.
                    await SaveAsync(profile);
                    await EngineOf(profile).ApplySnapshotAsync(snapshot, replaceLocalData: true);
                    InvalidateCaches();
                    background?.Schedule();
                    return SyncOutcome.Done;
                }
                catch (LanSyncException ex)
                {
                    await AbandonPairingAsync();
                    return OutcomeOf(ex);
                }
                catch
                {
                    await AbandonPairingAsync();
                    throw;
                }
            }
            finally
            {
                _busy.Release();
            }
        }

        private async Task AbandonPairingAsync()
        {
            await syncState.DisableAsync();
            try
            {
                await SaveAsync(null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _profile = null; // storage that could not even be cleared: nothing more to do here
            }
        }

        private static SyncOutcome OutcomeOf(LanSyncException ex) => ex.Failure switch
        {
            LanFailure.Unreachable => SyncOutcome.Unreachable,
            LanFailure.CertificateMismatch => SyncOutcome.WrongDevice,
            LanFailure.VersionMismatch => SyncOutcome.VersionMismatch,
            _ => SyncOutcome.NotRecognised
        };

        // ---- Syncing ----

        public async Task<SyncOutcome> SyncNowAsync()
        {
            if (await LoadAsync() is not { } profile)
                return SyncOutcome.Done;

            await _busy.WaitAsync();
            try
            {
                var engine = EngineOf(profile);
                var store = Store;
                var deviceId = await syncState.GetDeviceIdAsync();
                var outcome = SyncOutcome.Done;
                var reached = new List<string>();
                var moved = new Dictionary<string, SyncPeer>();
                var find = findComputer ?? (id => LanBeacon.FindAsync(id, TimeSpan.FromSeconds(2)));
                try
                {
                    await AttachmentHashes.FillAsync(database);
                    await engine.ExportAsync(store);
                    await engine.CompactAsync(store);

                    // A copy of the list: a device removed meanwhile is simply not updated at the end
                    foreach (var peer in profile.Peers.ToList())
                    {
                        try
                        {
                            var authSecret = Convert.FromBase64String(profile.AuthSecret);
                            LanSyncSession session;
                            try
                            {
                                session = await LanSyncSession.ConnectAsync(peer.Addresses, peer.Port, peer.Fingerprint, deviceId, authSecret);
                            }
                            catch (LanSyncException ex) when (ex.Failure == LanFailure.Unreachable)
                            {
                                // The computer may have another address now: ask the network, and remember it when it answers there
                                if (await find(peer.DeviceId) is not { } found)
                                    throw;
                                var current = peer with { Addresses = [found.Address], Port = found.Port };
                                session = await LanSyncSession.ConnectAsync(current.Addresses, current.Port, current.Fingerprint, deviceId, authSecret);
                                moved[peer.DeviceId] = current;
                            }
                            await using (session)
                            {
                                await SyncStores.CopyMissingAsync(store, session);
                                await SyncStores.CopyMissingAsync(session, store);
                            }
                            reached.Add(peer.DeviceId);
                        }
                        catch (LanSyncException ex)
                        {
                            outcome = OutcomeOf(ex);
                        }
                    }

                    // The shared folder (away from home, or the computer is off): the same copy, with the folder as the other store
                    // (Open() too is inside the try: resolving a saved folder is where a revoked permission shows. Platform stores throw their own
                    // exception types, so anything but a cancellation counts as "the folder is not available".)
                    ISyncStore? openedFolder = null;
                    if (sharedFolder != null)
                    {
                        try
                        {
                            if (await Task.Run(sharedFolder.Open) is { } shared)
                            {
                                await SyncStores.CopyMissingAsync(store, shared);
                                await SyncStores.CopyMissingAsync(shared, store);
                                openedFolder = shared;
                                // The computer not answering is no problem when the folder did the work (with several computers the
                                // last one's outcome wins, as before)
                                if (outcome == SyncOutcome.Unreachable)
                                    outcome = SyncOutcome.Done;
                            }
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            if (outcome is SyncOutcome.Done or SyncOutcome.Unreachable)
                                outcome = SyncOutcome.FolderUnavailable; // the user should hear about the folder even when the computer is also away
                        }
                    }

                    try
                    {
                        await engine.ApplyAsync(store);
                        await engine.PruneAsync(store);
                        // Files that a base file replaces are deleted in the folder too, or it would grow a file per sync for ever. After the
                        // apply, so a base applied in this run counts at once (the local copies are gone: they would be copied back next run)
                        if (openedFolder != null)
                            await TryPruneFolderAsync(engine, openedFolder);
                    }
                    catch (SyncVersionException)
                    {
                        outcome = SyncOutcome.VersionMismatch;
                    }
                }
                finally
                {
                    // What was reached is remembered even when a later step failed
                    InvalidateCaches();
                    await UpdateProfileAsync(current =>
                    {
                        var changed = false;
                        foreach (var id in reached)
                        {
                            var index = current.Peers.FindIndex(p => p.DeviceId == id);
                            if (index >= 0)
                            {
                                // Only what this run learned: the entry may have changed meanwhile
                                var peer = current.Peers[index];
                                if (moved.TryGetValue(id, out var m))
                                    peer = peer with { Addresses = m.Addresses, Port = m.Port };
                                current.Peers[index] = peer with { LastSync = _time.GetUtcNow() };
                                changed = true;
                            }
                        }
                        return changed;
                    });
                }
                return outcome;
            }
            finally
            {
                _busy.Release();
            }
        }

        public async Task<bool> PickFolderAsync()
        {
            if (sharedFolder is not { IsSupported: true } || await LoadAsync() == null)
                return false;
            return await sharedFolder.PickAsync();
        }

        public Task ClearFolderAsync()
        {
            sharedFolder?.Clear();
            return Task.CompletedTask;
        }

        /// <summary>Housekeeping of the shared folder: a failure here (a provider that refuses a delete) never fails the sync, the next run tries again.</summary>
        private static async Task TryPruneFolderAsync(SyncEngine engine, ISyncStore folder)
        {
            try
            {
                await engine.PruneAsync(folder);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
            }
        }

        /// <summary>Another device wrote change files into this device's store (it is listening): apply them.</summary>
        private async Task ApplyIncomingAsync()
        {
            if (await LoadAsync() is not { } profile)
                return;
            await _busy.WaitAsync();
            try
            {
                if (await EngineOf(profile).ApplyAsync(Store) > 0)
                {
                    InvalidateCaches();
                    Changed?.Invoke();
                }
            }
            catch (Exception ex) when (ex is SyncVersionException or CryptographicException or IOException)
            {
                // Wrong version or a damaged file: the next manual sync reports it
            }
            finally
            {
                _busy.Release();
            }
        }

        // ---- Turning off ----

        public Task RemovePeerAsync(string deviceId) =>
            UpdateProfileAsync(profile => profile.Devices.RemoveAll(d => d.DeviceId == deviceId) + profile.Peers.RemoveAll(p => p.DeviceId == deviceId) > 0);

        public async Task DisableAsync()
        {
            await _busy.WaitAsync();
            try
            {
                if (_beacon != null)
                {
                    await _beacon.DisposeAsync();
                    _beacon = null;
                }
                if (_server != null)
                {
                    await _server.DisposeAsync();
                    _server = null;
                }
                background?.Cancel();
                sharedFolder?.Clear();
                await syncState.DisableAsync();
                await SaveAsync(null);
                if (Directory.Exists(paths.SyncFolder))
                    Directory.Delete(paths.SyncFolder, recursive: true);
            }
            finally
            {
                _busy.Release();
            }
        }
    }
}
