using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>Platforms without the backup password yet (secure storage is Android only for now): plain backups.</summary>
    public sealed class NoBackupPasswordStore : IBackupPasswordStore
    {
        public bool IsSupported => false;

        public Task<string?> GetAsync() => Task.FromResult<string?>(null);

        public Task SetAsync(string? password) => throw new NotSupportedException("This platform can't keep a backup password.");
    }
}
