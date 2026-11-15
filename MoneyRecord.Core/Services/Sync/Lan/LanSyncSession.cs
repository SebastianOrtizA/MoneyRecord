using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text.Json;

namespace MoneyRecord.Services.Sync.Lan
{
    public enum LanFailure
    {
        /// <summary>No address answered (the PC is off, not on this network, or a firewall / guest Wi-Fi blocks it).</summary>
        Unreachable,

        /// <summary>Something answered with another certificate than the pairing code named.</summary>
        CertificateMismatch,

        /// <summary>The other device does not know this device (not paired, or the code expired or was used).</summary>
        Rejected,

        /// <summary>The other device speaks another version of the protocol.</summary>
        VersionMismatch
    }

    public sealed class LanSyncException(LanFailure failure, string message, Exception? inner = null) : Exception(message, inner)
    {
        public LanFailure Failure { get; } = failure;
    }

    /// <summary>
    /// A connection to the listening device. As an <see cref="ISyncStore"/> it is the other device's store, so
    /// <see cref="SyncStores.CopyMissingAsync"/> moves change files both ways, and the engine then applies them from the local store.
    /// </summary>
    public sealed class LanSyncSession : ISyncStore, IAsyncDisposable
    {
        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(4);
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(60);

        private readonly TcpClient _client;
        private readonly SslStream _tls;
        private readonly SemaphoreSlim _turn = new(1, 1);
        private Dictionary<string, long>? _list;

        private LanSyncSession(TcpClient client, SslStream tls, string peerDeviceId)
        {
            _client = client;
            _tls = tls;
            PeerDeviceId = peerDeviceId;
        }

        public string PeerDeviceId { get; }

        /// <summary>Connects to a device this one is already paired with (the sync key proves it).</summary>
        public static async Task<LanSyncSession> ConnectAsync(IEnumerable<string> addresses, int port, string fingerprint,
            string deviceId, byte[] syncKey, CancellationToken cancellation = default)
        {
            var (session, _) = await OpenAsync(addresses, port, fingerprint, deviceId, nonce => ("auth", LanSyncServer.Mac(syncKey, nonce, deviceId)), cancellation);
            return session;
        }

        /// <summary>Pairs with the device that showed the code: returns the connection and the sync key it handed over.</summary>
        public static async Task<(LanSyncSession Session, byte[] SyncKey)> PairAsync(LanPairingInfo info, string deviceId, CancellationToken cancellation = default)
        {
            var secret = Convert.FromBase64String(info.Secret);
            var (session, key) = await OpenAsync(info.Addresses, info.Port, info.Fingerprint, deviceId, nonce => ("pair", LanSyncServer.Mac(secret, nonce, deviceId)), cancellation);
            return (session, key ?? throw new LanSyncException(LanFailure.Rejected, "No key was handed over."));
        }

        private static async Task<(LanSyncSession, byte[]?)> OpenAsync(IEnumerable<string> addresses, int port, string fingerprint, string deviceId,
            Func<byte[], (string Op, byte[] Mac)> credentials, CancellationToken cancellation)
        {
            Exception? last = null;
            foreach (var address in addresses)
            {
                var client = new TcpClient();
                try
                {
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                    {
                        timeout.CancelAfter(ConnectTimeout);
                        await client.ConnectAsync(address, port, timeout.Token);
                    }
                    var tls = new SslStream(client.GetStream(), false, (_, certificate, _, _) =>
                        certificate != null && LanCertificate.FingerprintOf(certificate) == fingerprint);
                    await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                    {
                        TargetHost = "moneyrecord-sync",
                        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                    }, cancellation);

                    using var step = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                    step.CancelAfter(RequestTimeout);
                    await LanFrames.WriteAsync(tls, LanMessage.Of("hello", ("protocol", LanSyncServer.Protocol.ToString()), ("device", deviceId)), step.Token);
                    var hello = await LanFrames.ReadAsync(tls, step.Token);
                    if (!hello.Ok)
                        throw new LanSyncException(hello["error"] == "version" ? LanFailure.VersionMismatch : LanFailure.Rejected, "The device refused the connection.");

                    var (op, mac) = credentials(Convert.FromBase64String(hello["nonce"]));
                    await LanFrames.WriteAsync(tls, LanMessage.Of(op, ("device", deviceId), ("mac", Convert.ToBase64String(mac))), step.Token);
                    var accepted = await LanFrames.ReadAsync(tls, step.Token);
                    if (!accepted.Ok)
                        throw new LanSyncException(LanFailure.Rejected, "The device does not know this one.");

                    return (new LanSyncSession(client, tls, hello["device"]), accepted.Body);
                }
                catch (LanSyncException)
                {
                    client.Dispose();
                    throw;
                }
                catch (AuthenticationException ex)
                {
                    client.Dispose();
                    throw new LanSyncException(LanFailure.CertificateMismatch, "The certificate is not the one in the pairing code.", ex);
                }
                catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException or InvalidDataException or FormatException)
                {
                    client.Dispose();
                    cancellation.ThrowIfCancellationRequested();
                    last = ex;
                }
            }
            throw new LanSyncException(LanFailure.Unreachable, "The other device did not answer.", last);
        }

        private async Task<LanMessage> AskAsync(LanMessage request)
        {
            await _turn.WaitAsync();
            try
            {
                using var timeout = new CancellationTokenSource(RequestTimeout);
                await LanFrames.WriteAsync(_tls, request, timeout.Token);
                var reply = await LanFrames.ReadAsync(_tls, timeout.Token);
                if (!reply.Ok)
                    throw new LanSyncException(LanFailure.Rejected, $"The device refused: {reply["error"]}.");
                return reply;
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or InvalidDataException or ObjectDisposedException)
            {
                throw new LanSyncException(LanFailure.Unreachable, "The connection was lost.", ex);
            }
            finally
            {
                _turn.Release();
            }
        }

        private async Task<Dictionary<string, long>> ListAsync() =>
            _list ??= JsonSerializer.Deserialize<Dictionary<string, string>>((await AskAsync(LanMessage.Of("list")))["devices"])!
                .ToDictionary(d => d.Key, d => long.Parse(d.Value));

        public async Task<IReadOnlyList<string>> DevicesAsync() => [.. (await ListAsync()).Keys];

        public async Task<long> LastSeqAsync(string deviceId) => (await ListAsync()).GetValueOrDefault(deviceId);

        public async Task<byte[]?> ReadAsync(string deviceId, long seq)
        {
            var reply = await AskAsync(LanMessage.Of("get", ("device", deviceId), ("seq", seq.ToString())));
            return reply["found"] == "1" ? reply.Body : null;
        }

        public async Task WriteAsync(string deviceId, long seq, byte[] data)
        {
            await AskAsync(new LanMessage(LanMessage.Of("put", ("device", deviceId), ("seq", seq.ToString())).Header, data));
            _list = null;
        }

        /// <summary>The other device's whole data, for a device that is new or replaces its own (see <see cref="SyncEngine.ApplySnapshotAsync"/>).</summary>
        public async Task<byte[]> SnapshotAsync() =>
            (await AskAsync(LanMessage.Of("snapshot"))).Body ?? throw new LanSyncException(LanFailure.Rejected, "No snapshot.");

        public async ValueTask DisposeAsync()
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await LanFrames.WriteAsync(_tls, LanMessage.Of("bye"), timeout.Token);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
            {
                // Already gone
            }
            await _tls.DisposeAsync();
            _client.Dispose();
            _turn.Dispose();
        }
    }
}
