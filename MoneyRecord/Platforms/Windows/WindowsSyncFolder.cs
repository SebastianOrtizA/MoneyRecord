using CommunityToolkit.Maui.Storage;
using MoneyRecord.Services;
using MoneyRecord.Services.Sync;

namespace MoneyRecord.Platforms.Windows
{
    /// <summary>
    /// The shared sync folder as a plain path from the system folder picker (a Google Drive, OneDrive, Nextcloud or Syncthing folder
    /// kept by its desktop app). The change files live in <c>&lt;folder&gt;\MoneyRecordSync\&lt;device&gt;\&lt;seq&gt;.chg</c>.
    /// <see cref="Name"/> is null when the folder was deleted or moved.
    /// </summary>
    public sealed class WindowsSyncFolder(IPreferencesService preferences) : ISyncFolder
    {
        private const string FolderPathKey = "Sync.FolderPath";
        private const string RootName = "MoneyRecordSync";

        public bool IsSupported => true;

        private string? FolderPath => preferences.Get(FolderPathKey, string.Empty) is { Length: > 0 } path ? path : null;

        public string? Name => FolderPath is { } path && Directory.Exists(path) ? new DirectoryInfo(path).Name : null;

        public async Task<bool> PickAsync()
        {
            var result = await FolderPicker.Default.PickAsync(CancellationToken.None);
            if (!result.IsSuccessful || result.Folder is null)
                return false;
            preferences.Set(FolderPathKey, result.Folder.Path);
            return true;
        }

        public void Clear() => preferences.Set(FolderPathKey, string.Empty);

        public ISyncStore? Open() =>
            FolderPath is not { } path ? null
                : Directory.Exists(path) ? new DirectorySyncStore(Path.Combine(path, RootName))
                : throw new IOException("The sync folder is gone");
    }
}
