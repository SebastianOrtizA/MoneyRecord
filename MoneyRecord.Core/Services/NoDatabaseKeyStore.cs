using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>Platforms without a key store for the database (and the tests that don't encrypt): no encryption.</summary>
    public sealed class NoDatabaseKeyStore : IDatabaseKeyStore
    {
        public static readonly NoDatabaseKeyStore Instance = new();

        public bool IsSupported => false;

        public Task<string?> GetAsync() => Task.FromResult<string?>(null);

        public Task SetAsync(string key) => throw new NotSupportedException("This platform can't keep a database key.");

        public void Remove()
        {
        }
    }
}
