using System.Globalization;

namespace MoneyRecord.Services.Sync
{
    /// <summary>
    /// A plain folder: <c>root/&lt;device&gt;/&lt;seq&gt;.chg</c>. The device's own local store, and a shared folder on a desktop.
    /// A file is written under a temporary name and renamed, so a reader never sees half of it; files that do not follow the
    /// naming (a sync tool's "name (1).chg" copy) are ignored.
    /// </summary>
    public sealed class DirectorySyncStore(string root) : ISyncStore
    {
        private const string Extension = ".chg";

        private string FileOf(string deviceId, long seq) => Path.Combine(root, deviceId, $"{seq:D8}{Extension}");

        public Task<IReadOnlyList<string>> DevicesAsync() =>
            Task.FromResult<IReadOnlyList<string>>(Directory.Exists(root)
                ? Directory.GetDirectories(root).Select(d => Path.GetFileName(d)).ToList()
                : []);

        public Task<long> LastSeqAsync(string deviceId)
        {
            var folder = Path.Combine(root, deviceId);
            var last = Directory.Exists(folder)
                ? Directory.GetFiles(folder, "*" + Extension)
                    .Select(f => long.TryParse(Path.GetFileNameWithoutExtension(f), NumberStyles.None, CultureInfo.InvariantCulture, out var n)
                        && Path.GetFileName(f) == $"{n:D8}{Extension}" ? n : 0)
                    .DefaultIfEmpty(0).Max()
                : 0;
            return Task.FromResult(last);
        }

        public Task<byte[]?> ReadAsync(string deviceId, long seq)
        {
            var file = FileOf(deviceId, seq);
            return Task.FromResult(File.Exists(file) ? File.ReadAllBytes(file) : null);
        }

        public Task WriteAsync(string deviceId, long seq, byte[] data)
        {
            var file = FileOf(deviceId, seq);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            var temp = file + ".tmp";
            File.WriteAllBytes(temp, data);
            File.Move(temp, file, overwrite: true);
            return Task.CompletedTask;
        }
    }

    public static class SyncStores
    {
        /// <summary>Copies every file that <paramref name="to"/> lacks from <paramref name="from"/> (stops at a gap); returns how many.</summary>
        public static async Task<int> CopyMissingAsync(ISyncStore from, ISyncStore to)
        {
            var copied = 0;
            foreach (var device in await from.DevicesAsync())
            {
                var have = await to.LastSeqAsync(device);
                var last = await from.LastSeqAsync(device);
                for (var seq = have + 1; seq <= last; seq++)
                {
                    if (await from.ReadAsync(device, seq) is not { } data)
                        break;
                    await to.WriteAsync(device, seq, data);
                    copied++;
                }
            }
            return copied;
        }
    }
}
