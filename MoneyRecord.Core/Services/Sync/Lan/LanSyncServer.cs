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
    /// that the pairing code pins. A device proves it belongs with an HMAC of a fresh challenge made with the sync key; a new device
    /// proves it saw the QR code with an HMAC made with the one-time secret in it, and is then given the sync key. After that it
    /// can list, read and write change files in the device's local store, and read a snapshot.
    /// </summary>
    public sealed class LanSyncServer(
        X509Certificate2 certificate, byte[] syncKey, string deviceId, string deviceName, ISyncStore store,
        Func<Task<byte[]>> createSnapshot, TimeProvider? time = null) : IAsyncDisposable
    {
        public const int Protocol = 1;
        private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(60);

        private readonly TimeProvider _time = time ?? TimeProvider.System;
        private readonly List<(byte[] Secret, DateTimeOffset Expires)> _offers = [];
        private readonly SemaphoreSlim _storeLock = new(1, 1);
        private readonly CancellationTokenSource _stop = new();
        private TcpListener? _listener;
        private Task? _accepting;
        private bool _disposed;

        /// <summary>A device wrote change files into the store: they are ready to be applied.</summary>
        public event Action? StoreChanged;

        public int Port => ((IPEndPoint?)_listener?.LocalEndpoint)?.Port ?? 0;

        /// <summary>Starts listening (0 = any free port) and returns the port.</summary>
        public int Start(int port = 0)
        {
            _listener = new TcpListener(IPAddress.Any, port);
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
                    _ = Task.Run(() => HandleAsync(client));
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

                var hello = await LanFrames.ReadAsync(tls, ct);
                if (hello["op"] != "hello" || hello["protocol"] != Protocol.ToString())
                {
                    await LanFrames.WriteAsync(tls, Reply("error", "version"), ct);
                    return;
                }
                var nonce = RandomNumberGenerator.GetBytes(32);
                await LanFrames.WriteAsync(tls, new LanMessage(new() { ["ok"] = "1", ["nonce"] = Convert.ToBase64String(nonce), ["device"] = deviceId, ["protocol"] = Protocol.ToString() }), ct);

                var credentials = await LanFrames.ReadAsync(tls, ct);
                var peer = credentials["device"];
                if (!TryAuthenticate(credentials, nonce, peer, out var newKey))
                {
                    await LanFrames.WriteAsync(tls, Reply("error", "auth"), ct);
                    return;
                }
                await LanFrames.WriteAsync(tls, new LanMessage(new() { ["ok"] = "1" }, newKey ? syncKey : null), ct);

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

        /// <summary>True when the client is a paired device (HMAC with the sync key) or holds a valid pairing offer (then <paramref name="newKey"/>).</summary>
        private bool TryAuthenticate(LanMessage credentials, byte[] nonce, string peer, out bool newKey)
        {
            newKey = false;
            byte[] given;
            try { given = Convert.FromBase64String(credentials["mac"]); }
            catch (FormatException) { return false; }

            if (peer.Length == 0)
                return false;
            if (credentials["op"] == "auth")
                return CryptographicOperations.FixedTimeEquals(Mac(syncKey, nonce, peer), given);

            if (credentials["op"] != "pair")
                return false;
            lock (_offers)
            {
                _offers.RemoveAll(o => o.Expires < _time.GetUtcNow());
                var offer = _offers.FirstOrDefault(o => CryptographicOperations.FixedTimeEquals(Mac(o.Secret, nonce, peer), given));
                if (offer.Secret == null)
                    return false;
                _offers.Remove(offer); // good once
            }
            newKey = true;
            return true;
        }

        internal static byte[] Mac(byte[] key, byte[] nonce, string device) =>
            HMACSHA256.HashData(key, nonce.Concat(System.Text.Encoding.UTF8.GetBytes(device)).ToArray());

        private async Task<LanMessage> ServeAsync(LanMessage request)
        {
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
                    if (request.Body == null || !long.TryParse(request["seq"], out var seq) || !IsDeviceName(request["device"]))
                        return Reply("error", "bad");
                    await _storeLock.WaitAsync(_stop.Token);
                    try { await store.WriteAsync(request["device"], seq, request.Body); }
                    finally { _storeLock.Release(); }
                    StoreChanged?.Invoke();
                    return new LanMessage(new() { ["ok"] = "1" });
                }
                case "snapshot":
                    return new LanMessage(new() { ["ok"] = "1" }, await createSnapshot());
                default:
                    return Reply("error", "op");
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
            _stop.Dispose();
        }
    }
}
