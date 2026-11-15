using System.Security.Cryptography;
using System.Text.Json;
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

        /// <summary>The other device does not know this one, or the code expired or was used.</summary>
        NotRecognised,

        /// <summary>Something answered, but it is not the device in the code.</summary>
        WrongDevice,

        /// <summary>One of the two apps is older than the files the other writes.</summary>
        VersionMismatch
    }

    public sealed record SyncStatus(bool Supported, bool Enabled, bool CanListen, bool Listening, string DeviceName, IReadOnlyList<SyncPeer> Peers);

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

        Task RemovePeerAsync(string deviceId);

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
        Func<IReadOnlyList<string>>? localAddresses = null) : ISyncService
    {
        private const int PreferredPort = 48731;
        private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);

        private readonly TimeProvider _time = time ?? TimeProvider.System;
        private readonly SemaphoreSlim _busy = new(1, 1);
        private SyncProfile? _profile;
        private LanSyncServer? _server;

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
            _profile = profile;
            await secrets.SetAsync(profile == null ? null : JsonSerializer.Serialize(profile));
        }

        private SyncEngine EngineOf(SyncProfile profile) =>
            new(database, syncState, Convert.FromBase64String(profile.Key), _time);

        private void InvalidateCaches()
        {
            accounts.InvalidateCache();
            categories.InvalidateCache();
        }

        public async Task<SyncStatus> GetStatusAsync()
        {
            var profile = await LoadAsync();
            return new SyncStatus(secrets.IsSupported, profile != null, platform.CanListen, _server != null,
                profile?.DeviceName ?? platform.DeviceName, profile?.Peers ?? []);
        }

        // ---- Turning on ----

        public async Task EnableAsync()
        {
            if (!secrets.IsSupported || !platform.CanListen)
                throw new InvalidOperationException("This device cannot listen for others.");
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

        public async Task ResumeAsync()
        {
            if (_server == null && platform.CanListen && await LoadAsync() is { Certificate: not null } profile && await syncState.IsEnabledAsync())
                await StartListeningAsync(profile);
        }

        private async Task StartListeningAsync(SyncProfile profile)
        {
            if (_server != null || profile.Certificate == null)
                return;
            var engine = EngineOf(profile);
            var server = new LanSyncServer(LanCertificate.Load(Convert.FromBase64String(profile.Certificate)), Convert.FromBase64String(profile.Key),
                await syncState.GetDeviceIdAsync(), profile.DeviceName, Store, engine.CreateSnapshotAsync, _time);
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
                    var (session, key) = await LanSyncSession.PairAsync(info, deviceId);
                    byte[] snapshot;
                    await using (session)
                        snapshot = await session.SnapshotAsync();

                    var profile = new SyncProfile
                    {
                        Key = Convert.ToBase64String(key),
                        DeviceName = platform.DeviceName,
                        Peers = [new SyncPeer(info.DeviceId, info.DeviceName, info.Addresses, info.Port, info.Fingerprint, _time.GetUtcNow())]
                    };
                    await EngineOf(profile).ApplySnapshotAsync(snapshot, replaceLocalData: true);
                    InvalidateCaches();
                    await SaveAsync(profile);
                    return SyncOutcome.Done;
                }
                catch (LanSyncException ex)
                {
                    await syncState.DisableAsync();
                    return OutcomeOf(ex);
                }
                catch
                {
                    await syncState.DisableAsync();
                    throw;
                }
            }
            finally
            {
                _busy.Release();
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
                await engine.ExportAsync(store);

                foreach (var peer in profile.Peers.ToList())
                {
                    try
                    {
                        await using var session = await LanSyncSession.ConnectAsync(peer.Addresses, peer.Port, peer.Fingerprint, deviceId, Convert.FromBase64String(profile.Key));
                        await SyncStores.CopyMissingAsync(store, session);
                        await SyncStores.CopyMissingAsync(session, store);
                        profile.Peers[profile.Peers.IndexOf(peer)] = peer with { LastSync = _time.GetUtcNow() };
                    }
                    catch (LanSyncException ex)
                    {
                        outcome = OutcomeOf(ex);
                    }
                }

                try
                {
                    await engine.ApplyAsync(store);
                }
                catch (SyncVersionException)
                {
                    outcome = SyncOutcome.VersionMismatch;
                }
                InvalidateCaches();
                await SaveAsync(profile);
                return outcome;
            }
            finally
            {
                _busy.Release();
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

        public async Task RemovePeerAsync(string deviceId)
        {
            if (await LoadAsync() is { } profile)
            {
                profile.Peers.RemoveAll(p => p.DeviceId == deviceId);
                await SaveAsync(profile);
            }
        }

        public async Task DisableAsync()
        {
            await _busy.WaitAsync();
            try
            {
                if (_server != null)
                {
                    await _server.DisposeAsync();
                    _server = null;
                }
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
