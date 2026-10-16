using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>Platforms without a backup folder yet (iOS, Mac Catalyst, Windows): Settings hides automatic backups.</summary>
    public sealed class NoBackupFolder : IBackupFolder
    {
        public bool IsSupported => false;

        public string? Name => null;

        public Task<bool> PickAsync() => Task.FromResult(false);

        public IReadOnlyList<string> ListFiles() => [];

        public void Write(string fileName, string sourcePath) => throw new NotSupportedException();

        public void Delete(string fileName) => throw new NotSupportedException();
    }
}
