using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>
    /// MAUI's launcher hands the file to the viewer the system picks (a PDF reader, the gallery). An encrypted receipt goes
    /// out as a decrypted copy in the cache, which <see cref="IReceiptStore.ClearViewFolder"/> removes at start and when the app locks.
    /// </summary>
    public sealed class FileOpener(IReceiptStore receipts) : IFileOpener
    {
        public async Task OpenAsync(string path, string title)
        {
            var readable = await receipts.GetReadablePathAsync(path);
            await Launcher.Default.OpenAsync(new OpenFileRequest(title, new ReadOnlyFile(readable)));
        }
    }
}
