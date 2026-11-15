using System.Buffers.Binary;
using System.Text.Json;

namespace MoneyRecord.Services.Sync.Lan
{
    /// <summary>
    /// One message of the local-network link: a small header of text values (the operation and its arguments) and an optional
    /// binary body (a change file, a snapshot, the sync key). On the wire: header length, header JSON, body length (-1 = none), body.
    /// </summary>
    public sealed record LanMessage(Dictionary<string, string> Header, byte[]? Body = null)
    {
        public string this[string key] => Header.TryGetValue(key, out var value) ? value : "";

        public static LanMessage Of(string op, params (string Key, string Value)[] args) =>
            new(new Dictionary<string, string>(args.ToDictionary(a => a.Key, a => a.Value)) { ["op"] = op });

        public bool Ok => this["ok"] == "1";
    }

    public static class LanFrames
    {
        /// <summary>The largest header or body accepted; the biggest real content is a snapshot of years of data.</summary>
        public const int MaxSize = 64 * 1024 * 1024;

        public static async Task WriteAsync(Stream stream, LanMessage message, CancellationToken cancellation)
        {
            var header = JsonSerializer.SerializeToUtf8Bytes(message.Header);
            var buffer = new byte[4 + header.Length + 4 + (message.Body?.Length ?? 0)];
            BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(0, 4), header.Length);
            header.CopyTo(buffer, 4);
            BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(4 + header.Length, 4), message.Body?.Length ?? -1);
            message.Body?.CopyTo(buffer, 8 + header.Length);
            await stream.WriteAsync(buffer, cancellation);
            await stream.FlushAsync(cancellation);
        }

        /// <param name="maxHeader">Larger headers are refused before anything is allocated (a client not yet signed in gets a small limit).</param>
        /// <param name="maxBody">Larger bodies are refused the same way.</param>
        public static async Task<LanMessage> ReadAsync(Stream stream, CancellationToken cancellation, int maxHeader = 1024 * 1024, int maxBody = MaxSize)
        {
            var headerLength = await ReadLengthAsync(stream, cancellation);
            if (headerLength < 2 || headerLength > maxHeader)
                throw new InvalidDataException("Bad header length.");
            var header = JsonSerializer.Deserialize<Dictionary<string, string>>(await ReadExactAsync(stream, headerLength, cancellation))
                ?? throw new InvalidDataException("Bad header.");

            var bodyLength = await ReadLengthAsync(stream, cancellation);
            if (bodyLength < -1 || bodyLength > maxBody)
                throw new InvalidDataException("Bad body length.");
            return new LanMessage(header, bodyLength < 0 ? null : await ReadExactAsync(stream, bodyLength, cancellation));
        }

        private static async Task<int> ReadLengthAsync(Stream stream, CancellationToken cancellation) =>
            BinaryPrimitives.ReadInt32BigEndian(await ReadExactAsync(stream, 4, cancellation));

        private static async Task<byte[]> ReadExactAsync(Stream stream, int count, CancellationToken cancellation)
        {
            var buffer = new byte[count];
            await stream.ReadExactlyAsync(buffer, cancellation);
            return buffer;
        }
    }
}
