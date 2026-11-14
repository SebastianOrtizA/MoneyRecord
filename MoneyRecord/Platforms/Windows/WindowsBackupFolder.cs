using CommunityToolkit.Maui.Storage;
using Microsoft.Extensions.Logging;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Platforms.Windows
{
    /// <summary>
    /// The backup folder as a plain folder path from the system folder picker (no storage access framework on Windows):
    /// any folder the user can write to, including one a sync app (OneDrive, Nextcloud, ...) keeps in the cloud.
    /// <see cref="Name"/> is null when the folder was deleted or moved.
    /// </summary>
    public sealed class WindowsBackupFolder(IPreferencesService preferences, ILogger<WindowsBackupFolder> logger) : IBackupFolder
    {
        private const string FolderPathKey = "AutoBackup.FolderPath";

        public bool IsSupported => true;

        private string? Path => preferences.Get(FolderPathKey, string.Empty) is { Length: > 0 } path ? path : null;

        public string? Name => Path is { } path && Directory.Exists(path) ? new DirectoryInfo(path).Name : null;

        public async Task<bool> PickAsync()
        {
            var result = await FolderPicker.Default.PickAsync(CancellationToken.None);
            if (!result.IsSuccessful || result.Folder is null)
                return false;
            preferences.Set(FolderPathKey, result.Folder.Path);
            return true;
        }

        public IReadOnlyList<string> ListFiles() =>
            Directory.EnumerateFiles(RequirePath()).Select(System.IO.Path.GetFileName).OfType<string>().ToList();

        public void Write(string fileName, string sourcePath)
        {
            var target = System.IO.Path.Combine(RequirePath(), System.IO.Path.GetFileName(fileName));
            // No half-written backup left in the folder: copied under another name, then renamed
            var partial = target + ".part";
            try
            {
                File.Copy(sourcePath, partial, overwrite: true);
                File.Move(partial, target, overwrite: true);
            }
            catch
            {
                try
                {
                    File.Delete(partial);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete an unfinished backup");
                }
                throw;
            }
        }

        public void Delete(string fileName) =>
            File.Delete(System.IO.Path.Combine(RequirePath(), System.IO.Path.GetFileName(fileName)));

        private string RequirePath() =>
            Path is { } path && Directory.Exists(path) ? path : throw new IOException("No backup folder is chosen");
    }
}
