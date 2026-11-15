using System.Text;
using System.Text.Json;

namespace MoneyRecord.Services.Sync.Lan
{
    /// <summary>
    /// What the device that shows the QR code tells the one that scans it: where to connect, the fingerprint of the certificate to
    /// expect (so nobody else on the network can pose as it), and a one-time secret that proves the scanner saw the code. The text
    /// form is what the QR contains, and what can be typed when there is no camera.
    /// </summary>
    public sealed record LanPairingInfo(string[] Addresses, int Port, string Fingerprint, string Secret, long ExpiresUnix, string DeviceId, string DeviceName)
    {
        private const string Prefix = "moneyrecord-sync:1:";

        public string ToText() => Prefix + Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(this)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        /// <summary>Null when the text is not a pairing code of this version.</summary>
        public static LanPairingInfo? TryParse(string? text)
        {
            if (text == null || !text.StartsWith(Prefix, StringComparison.Ordinal))
                return null;
            try
            {
                var base64 = text[Prefix.Length..].Replace('-', '+').Replace('_', '/');
                base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
                var info = JsonSerializer.Deserialize<LanPairingInfo>(Encoding.UTF8.GetString(Convert.FromBase64String(base64)));
                return info is { Addresses.Length: > 0, Port: > 0 and < 65536 } && info.Fingerprint.Length > 0 && info.Secret.Length > 0 ? info : null;
            }
            catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
            {
                return null;
            }
        }
    }
}
