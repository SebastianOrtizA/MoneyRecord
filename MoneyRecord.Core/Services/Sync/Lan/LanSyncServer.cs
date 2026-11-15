using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace MoneyRecord.Services.Sync.Lan
{
    /// <summary>
    /// The listening side of the local-network link (the Windows app while it is open). Speaks TLS with a self-signed certificate
    /// that the pairing code pins. A paired device proves itself with an HMAC of a fresh challenge made with its own secret (kept in the
    /// <see cref="ILanDeviceRegistry"/>, so removing it locks it out); a new device proves it saw the QR code with an HMAC made with
    /// the one-time secret in it, and is then given the sync key and its own secret. After that it
    /// can list, read and write change files in the device's local store, and read a snapshot.
    /// </summary>
    public sealed class LanSyncServer(
        X509Certificate2 certificate, byte[] syncKey, string deviceId, string deviceName, ISyncStore store,
        Func<Task<byte[]>> createSnapshot, ILanDeviceRegistry registry, TimeProvider? time = null, IBlobStore? blobs = null) : IAsyncDisposable
    {
        public const int Protocol = 1;
        private const int MaxConnections = 16;
        /// <summary>Before a client has proved who it is, nothing larger than this is read from it (and no body at all).</summary>
        private const int PreAuthHeaderSize = 4096;
        private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(60);

        private readonly TimeProvider _time = time ?? TimeProvider.System;
        private readonly List<(byte[] Secret, DateTimeOffset Expires)> _offers = [];
        // Reads need no lock: the store writes a file under a temporary name and renames it, so a reader sees all of a file or none
        private readonly SemaphoreSlim _storeLock = new(1, 1);
        private readonly SemaphoreSlim _connections = new(MaxConnections, MaxConnections);
        private readonly System.Collections.Concurrent.ConcurrentDictionary<Task, byte> _handlers = new();
        private readonly CancellationTokenSource _stop = new();
        private TcpListener? _listener;
        private Task? _accepting;
        private bool _disposed;

        /// <summary>A device wrote change files into the store: they are ready to be applied.</summary>
        public event Action? StoreChanged;

        public int Port => ((IPEndPoint?)_listener?.LocalEndpoint)?.Port ?? 0;

        /// <summary>Starts listening (0 = any free port; on all addresses unless one is given) and returns the port.</summary>
        public int Start(int port = 0, IPAddress? address = null)
        {
            _listener = new TcpListener(address ?? IPAddress.Any, port);
            _listener.Start();
            _accepting = Task.Run(AcceptLoopAsync);
            return Port;
        }

        /// <summary>A pairing code valid once, for <paramref name="validFor"/>, naming the addresses this device can be reached at.</summary>
        public LanPairingInfo CreatePairingOffer(IEnumerable<string> addresses, TimeSpan validFor)
        {
            var secret = RandomNumberGenerator.GetBytes(16);
            var expires = _time.GetUtcNow() + validFor;
            lock (_offers)
            {
                _offers.RemoveAll(o => o.Expires < _time.GetUtcNow());
                _offers.Add((secret, expires));
            }
            return new LanPairingInfo([.. addresses], Port, LanCertificate.FingerprintOf(certificate), Convert.ToBase64String(secret),
                expires.ToUnixTimeSeconds(), deviceId, deviceName);
        }

        private async Task AcceptLoopAsync()
        {
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener!.AcceptTcpClientAsync(_stop.Token);
                    if (!_connections.Wait(0))
                    {
                        client.Dispose(); // too many at once: a device that is not paired should not be able to use up memory
                        continue;
                    }
                    var handler = Task.Run(async () =>
                    {
                        try { await HandleAsync(client); }
                        finally { _connections.Release(); }
                    });
                    _handlers[handler] = 0;
                    _ = handler.ContinueWith(t => _handlers.TryRemove(t, out _), TaskScheduler.Default);
                }
                catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
                {
                    return;
                }
            }
        }

        private async Task HandleAsync(TcpClient client)
        {
            using var _ = client;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            try
            {
                timeout.CancelAfter(IdleTimeout);
                var ct = timeout.Token;
                await using var tls = new SslStream(client.GetStream());
                await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ServerCertificate = certificate,
                    ClientCertificateRequired = false,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                }, ct);

                var hello = await LanFrames.ReadAsync(tls, ct, PreAuthHeaderSize, maxBody: 0);
                if (hello["op"] != "hello" || hello["protocol"] != Protocol.ToString())
                {
                    await LanFrames.WriteAsync(tls, Reply("error", "version"), ct);
                    return;
                }
                var nonce = RandomNumberGenerator.GetBytes(32);
                await LanFrames.WriteAsync(tls, new LanMessage(new() { ["ok"] = "1", ["nonce"] = Convert.ToBase64String(nonce), ["device"] = deviceId, ["protocol"] = Protocol.ToString() }), ct);

                var credentials = await LanFrames.ReadAsync(tls, ct, PreAuthHeaderSize, maxBody: 0);
                var peer = credentials["device"];
                var (accepted, handover) = await AuthenticateAsync(credentials, nonce, peer);
                if (!accepted)
                {
                    await LanFrames.WriteAsync(tls, Reply("error", "auth"), ct);
                    return;
                }
                await LanFrames.WriteAsync(tls, new LanMessage(new() { ["ok"] = "1" }, handover), ct);

                while (true)
                {
                    timeout.CancelAfter(IdleTimeout);
                    var request = await LanFrames.ReadAsync(tls, ct);
                    if (request["op"] == "bye")
                        return;
                    await LanFrames.WriteAsync(tls, await ServeAsync(request), ct);
                }
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or AuthenticationException or InvalidDataException
                or SocketException or ObjectDisposedException or JsonException or EndOfStreamException)
            {
                // A dropped connection, a client that gave up, or one that is not speaking this protocol: nothing to do
            }
        }

        private static LanMessage Reply(string key, string value) => new(new() { ["ok"] = "0", [key] = value });

        /// <summary>
        /// Accepts a paired device (HMAC with its own secret), or a new one that holds a valid pairing offer: it is registered, and
        /// <c>Handover</c> is the sync key followed by its own secret.
        /// </summary>
        private async Task<(bool Accepted, byte[]? Handover)> AuthenticateAsync(LanMessage credentials, byte[] nonce, string peer)
        {
            byte[] given;
            try { given = Convert.FromBase64String(credentials["mac"]); }
            catch (FormatException) { return (false, null); }

            if (!IsDeviceName(peer))
                return (false, null);
            if (credentials["op"] == "auth")
            {
                var secret = await registry.SecretOfAsync(peer);
                if (secret == null || !CryptographicOperations.FixedTimeEquals(Mac(secret, "auth", nonce, peer), given))
                    return (false, null);
                await registry.SeenAsync(peer);
                return (true, null);
            }

            if (credentials["op"] != "pair")
                return (false, null);
            lock (_offers)
            {
                _offers.RemoveAll(o => o.Expires < _time.GetUtcNow());
                var offer = _offers.FirstOrDefault(o => CryptographicOperations.FixedTimeEquals(Mac(o.Secret, "pair", nonce, peer), given));
                if (offer.Secret == null)
                    return (false, null);
                _offers.Remove(offer); // good once
            }
            var own = await registry.RegisterAsync(peer, credentials["name"]);
            return (true, [.. syncKey, .. own]);
        }
        /// <summary>HMAC over the operation, the challenge and the device id, each length-prefixed (a MAC made for pairing is no use for signing in, and the parts cannot run into each other).</summary>
        internal static byte[] Mac(byte[] key, string op, byte[] nonce, string device)
        {
            using var input = new MemoryStream();
            using var writer = new BinaryWriter(input);
            writer.Write(op);
            writer.Write(nonce.Length);
            writer.Write(nonce);
            writer.Write(device);
            writer.Flush();
            return HMACSHA256.HashData(key, input.ToArray());
        }

        private async Task<LanMessage> ServeAsync(LanMessage request)
        {
            if (request["op"] is "get" or "put" && !IsDeviceName(request["device"]))
                return Reply("error", "bad");

            switch (request["op"])
            {
                case "list":
                {
                    var devices = new Dictionary<string, string>();
                    foreach (var device in await store.DevicesAsync())
                        devices[device] = (await store.LastSeqAsync(device)).ToString();
                    return new LanMessage(new() { ["ok"] = "1", ["devices"] = JsonSerializer.Serialize(devices) });
                }
                case "get":
                {
                    var data = long.TryParse(request["seq"], out var seq) ? await store.ReadAsync(request["device"], seq) : null;
                    return new LanMessage(new() { ["ok"] = "1", ["found"] = data == null ? "0" : "1" }, data);
                }
                case "put":
                {
                    if (request.Body == null || !long.TryParse(request["seq"], out var seq))
                        return Reply("error", "bad");
                    await _storeLock.WaitAsync(_stop.Token);
                    try { await store.WriteAsync(request["device"], seq, request.Body); }
                    finally { _storeLock.Release(); }
                    StoreChanged?.Invoke();
                    return new LanMessage(new() { ["ok"] = "1" });
                }
                case "snapshot":
                    return new LanMessage(new() { ["ok"] = "1" }, await createSnapshot());
                case "blob-has" or "blob-get" or "blob-put":
                    return await ServeBlobAsync(request);
                default:
                    return Reply("error", "op");
            }
        }

        /// <summary>Receipt blobs (sealed, so this device holds nothing it can read): present, read, write. The name is a hash, never a path.</summary>
        private async Task<LanMessage> ServeBlobAsync(LanMessage request)
        {
            if (blobs == null)
                return Reply("error", "op");
            var hash = request["hash"];
            if (!SyncBlobs.IsHash(hash))
                return Reply("error", "bad");

            switch (request["op"])
            {
                case "blob-has":
                    return new LanMessage(new() { ["ok"] = "1", ["found"] = await blobs.HasAsync(hash) ? "1" : "0" });
                case "blob-get":
                {
                    var data = await blobs.ReadBlobAsync(hash);
                    return new LanMessage(new() { ["ok"] = "1", ["found"] = data == null ? "0" : "1" }, data);
                }
                default:
                {
                    if (request.Body == null || request.Body.Length > SyncBlobs.MaxSealedBytes || SyncBlobs.Open(syncKey, hash, request.Body) == null)
                        return Reply("error", "bad");
                    await _storeLock.WaitAsync(_stop.Token);
                    try { await blobs.WriteBlobAsync(hash, request.Body); }
                    finally { _storeLock.Release(); }
                    return new LanMessage(new() { ["ok"] = "1" });
                }
            }
        }

        /// <summary>A device id is a folder name in the store: nothing that could reach outside it.</summary>
        internal static bool IsDeviceName(string name) => name.Length is > 0 and <= 64 && name.All(char.IsAsciiLetterOrDigit);

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;
            _disposed = true;
            await _stop.CancelAsync();
            _listener?.Stop();
            if (_accepting != null)
                await _accepting;
            await Task.WhenAny(Task.WhenAll(_handlers.Keys), Task.Delay(TimeSpan.FromSeconds(2)));
            _stop.Dispose();
        }
    }
}
