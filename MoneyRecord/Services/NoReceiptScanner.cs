using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>Platforms without receipt reading yet (iOS, Mac Catalyst, Windows): a photo is only attached.</summary>
    public sealed class NoReceiptScanner : IReceiptScanner
    {
        public bool IsSupported => false;

        public Task<IReadOnlyList<ReceiptLine>> ReadLinesAsync(string imagePath) =>
            Task.FromResult<IReadOnlyList<ReceiptLine>>([]);
    }
}
