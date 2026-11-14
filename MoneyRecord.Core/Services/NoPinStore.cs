using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>Platforms without secure storage yet: no PIN.</summary>
    public sealed class NoPinStore : IPinStore
    {
        public bool IsSupported => false;

        public Task<string?> GetHashAsync() => Task.FromResult<string?>(null);

        public Task SetHashAsync(string? hash) => throw new NotSupportedException("This platform can't keep a PIN.");

        public Task<string?> GetLockoutAsync() => Task.FromResult<string?>(null);

        public Task SetLockoutAsync(string? state) => Task.CompletedTask;
    }
}
