using System.Security.Cryptography;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// Salted PBKDF2 hash of a PIN: the store keeps "v1.iterations.salt.hash" (base64), never the PIN. Comparison takes the
    /// same time whatever the digits.
    /// </summary>
    public static class PinHash
    {
        private const int Iterations = 120_000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        public static string Create(string pin)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var hash = Derive(pin, salt, Iterations);
            return $"v1.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        public static bool Verify(string pin, string? stored)
        {
            var parts = stored?.Split('.');
            if (parts is not { Length: 4 } || parts[0] != "v1" || !int.TryParse(parts[1], out var iterations) || iterations < 1)
                return false;
            try
            {
                var expected = Convert.FromBase64String(parts[3]);
                return CryptographicOperations.FixedTimeEquals(Derive(pin, Convert.FromBase64String(parts[2]), iterations), expected);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static byte[] Derive(string pin, byte[] salt, int iterations) =>
            Rfc2898DeriveBytes.Pbkdf2(pin, salt, iterations, HashAlgorithmName.SHA256, HashSize);
    }
}
