using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>Platforms without a QR scanner: the sync code is typed or pasted.</summary>
    public sealed class NoQrScanner : IQrScanner
    {
        public bool IsSupported => false;

        public Task<string?> ScanAsync() => Task.FromResult<string?>(null);
    }
}
