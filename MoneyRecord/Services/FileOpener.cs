using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>MAUI's launcher hands the file to the viewer the system picks (a PDF reader, the gallery).</summary>
    public sealed class FileOpener : IFileOpener
    {
        public Task OpenAsync(string path, string title) =>
            Launcher.Default.OpenAsync(new OpenFileRequest(title, new ReadOnlyFile(path)));
    }
}
