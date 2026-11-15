using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace MoneyRecord.Services.Sync.Lan
{
    /// <summary>
    /// The self-signed certificate a device listens with. Nobody validates it against an authority: the device that scans the QR code
    /// learns its fingerprint there and accepts only a certificate with that fingerprint.
    /// </summary>
    public static class LanCertificate
    {
        /// <summary>A new certificate and its PFX bytes (to keep in secure storage; the private key is in it).</summary>
        public static (X509Certificate2 Certificate, byte[] Pfx) Create()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var request = new CertificateRequest("CN=moneyrecord-sync", key, HashAlgorithmName.SHA256);
            using var selfSigned = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(20));
            var pfx = selfSigned.Export(X509ContentType.Pfx);
            return (Load(pfx), pfx);
        }

        /// <summary>Opened the way Windows' TLS needs it: with the key kept as a normal exportable one, not an ephemeral one.</summary>
        public static X509Certificate2 Load(byte[] pfx) =>
            X509CertificateLoader.LoadPkcs12(pfx, null, X509KeyStorageFlags.Exportable);

        public static string FingerprintOf(X509Certificate certificate) =>
            Convert.ToHexString(SHA256.HashData(certificate.GetRawCertData()));
    }
}
