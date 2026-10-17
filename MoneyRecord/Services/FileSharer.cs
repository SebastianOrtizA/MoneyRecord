using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>Files to share go to the cache folder; MAUI's share sheet sends them.</summary>
    public sealed class FileSharer : IFileSharer
    {
        public string NewCacheFile(string prefix, string extension)
        {
            foreach (var old in Directory.GetFiles(FileSystem.CacheDirectory, $"{prefix}_*{extension}"))
                File.Delete(old);
            return Path.Combine(FileSystem.CacheDirectory, $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}");
        }

        public Task ShareAsync(string path, string title) =>
            Share.Default.RequestAsync(new ShareFileRequest { Title = title, File = new ShareFile(path) });

        public Task ShareTextAsync(string text, string title) =>
            Share.Default.RequestAsync(new ShareTextRequest { Title = title, Text = text });
    }
}
