using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MoneyRecord.Services.Sync.Lan
{
    /// <summary>
    /// Finding the computer again when its address changed (a new DHCP lease, another router). The phone broadcasts a probe naming the
    /// computer's device id; the computer answers only to its own id, with the port it listens on. The sender's address of the answer
    /// is the new address. It reveals nothing a stranger could use: the id and port are not secrets, and the TLS pin and the
    /// device's own secret still decide who gets in.
    /// </summary>
    public sealed class LanBeacon : IAsyncDisposable
    {
        public const int DefaultPort = 48732;
        private const string ProbePrefix = "moneyrecord-sync?1:";
        private const string ReplyPrefix = "moneyrecord-sync!1:";

        private readonly string _deviceId;
        private readonly Func<int> _tcpPort;
        private readonly CancellationTokenSource _stop = new();
        private UdpClient? _udp;
        private Task? _loop;
        private int _disposed;

        public LanBeacon(string deviceId, Func<int> tcpPort)
        {
            _deviceId = deviceId;
            _tcpPort = tcpPort;
        }

        /// <summary>Starts answering probes. False when the port is taken (the computer is then found by its last address only).</summary>
        public bool Start(int port = DefaultPort, IPAddress? address = null)
        {
            try
            {
                _udp = new UdpClient(new IPEndPoint(address ?? IPAddress.Any, port));
            }
            catch (SocketException)
            {
                return false;
            }
            _loop = Task.Run(AnswerLoopAsync);
            return true;
        }

        public int Port => ((IPEndPoint?)_udp?.Client.LocalEndPoint)?.Port ?? 0;

        private async Task AnswerLoopAsync()
        {
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    var received = await _udp!.ReceiveAsync(_stop.Token);
                    var text = Encoding.UTF8.GetString(received.Buffer);
                    if (text == ProbePrefix + _deviceId)
                    {
                        var reply = Encoding.UTF8.GetBytes($"{ReplyPrefix}{_deviceId}:{_tcpPort()}");
                        await _udp.SendAsync(reply, received.RemoteEndPoint, _stop.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException)
                {
                    // One bad datagram (or an ICMP error for an answer) must not end the beacon
                }
            }
        }

        /// <summary>
        /// Asks the local network where the device is. <paramref name="target"/> is the broadcast address by default.
        /// Returns the address and port, or null when nothing answered within <paramref name="timeout"/>.
        /// </summary>
        public static async Task<(string Address, int Port)?> FindAsync(string deviceId, TimeSpan timeout, IPEndPoint? target = null, CancellationToken cancellation = default)
        {
            using var udp = new UdpClient { EnableBroadcast = true };
            var probe = Encoding.UTF8.GetBytes(ProbePrefix + deviceId);
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            limit.CancelAfter(timeout);
            try
            {
                await udp.SendAsync(probe, target ?? new IPEndPoint(IPAddress.Broadcast, DefaultPort), limit.Token);
                while (true)
                {
                    var received = await udp.ReceiveAsync(limit.Token);
                    var text = Encoding.UTF8.GetString(received.Buffer);
                    var expected = ReplyPrefix + deviceId + ":";
                    if (text.StartsWith(expected, StringComparison.Ordinal) && int.TryParse(text[expected.Length..], out var port) && port is > 0 and <= 65535)
                        return (received.RemoteEndPoint.Address.ToString(), port);
                }
            }
            catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
            {
                return null;
            }
            catch (SocketException)
            {
                return null; // no network at all, or broadcasting is not allowed here
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;
            await _stop.CancelAsync();
            _udp?.Dispose();
            if (_loop != null)
                await _loop;
            _stop.Dispose();
        }
    }
}
