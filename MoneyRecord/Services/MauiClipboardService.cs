using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class MauiClipboardService : IClipboardService
    {
        public Task SetTextAsync(string text) => Clipboard.Default.SetTextAsync(text);
    }
}
