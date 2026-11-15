using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace MoneyRecord.Services.Sync
{
    /// <summary>gzip then AES-256-GCM. The file's name is authenticated, so a file moved or renamed fails to open.</summary>
    public static class SyncCrypto
    {
        private const byte Version = 1;
        private const int NonceSize = 12, TagSize = 16, HeaderSize = 1 + NonceSize + TagSize;

        public static byte[] NewKey() => RandomNumberGenerator.GetBytes(32);

        public static byte[] Seal(byte[] key, string name, byte[] plain)
        {
            using var packed = new MemoryStream();
            using (var gzip = new GZipStream(packed, CompressionLevel.Optimal, leaveOpen: true))
                gzip.Write(plain);
            var data = packed.ToArray();

            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var tag = new byte[TagSize];
            var cipher = new byte[data.Length];
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, data, cipher, tag, Encoding.UTF8.GetBytes(name));

            var result = new byte[HeaderSize + cipher.Length];
            result[0] = Version;
            nonce.CopyTo(result, 1);
            tag.CopyTo(result, 1 + NonceSize);
            cipher.CopyTo(result, HeaderSize);
            return result;
        }

        /// <exception cref="CryptographicException">Wrong key, another name, a changed or unknown file.</exception>
        public static byte[] Open(byte[] key, string name, byte[] sealedData)
        {
            if (sealedData.Length < HeaderSize || sealedData[0] != Version)
                throw new CryptographicException("Unknown sync file format.");

            var data = new byte[sealedData.Length - HeaderSize];
            using (var aes = new AesGcm(key, TagSize))
                aes.Decrypt(sealedData.AsSpan(1, NonceSize), sealedData.AsSpan(HeaderSize), sealedData.AsSpan(1 + NonceSize, TagSize), data, Encoding.UTF8.GetBytes(name));

            using var gzip = new GZipStream(new MemoryStream(data), CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }
    }
}
