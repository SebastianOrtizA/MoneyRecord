using System.Globalization;

namespace MoneyRecord.Services.Sync
{
    /// <summary>
    /// A plain folder: <c>root/&lt;device&gt;/&lt;seq&gt;.chg</c>. The device's own local store, and a shared folder on a desktop.
    /// A file is written under a temporary name and renamed, so a reader never sees half of it; files that do not follow the
    /// naming (a sync tool's "name (1).chg" copy) are ignored.
    /// </summary>
    public sealed class DirectorySyncStore(string root) : IPrunableSyncStore
    {
        private const string Extension = ".chg";

        private string FileOf(string deviceId, long seq) => Path.Combine(root, deviceId, $"{seq:D8}{Extension}");

        public Task<IReadOnlyList<string>> DevicesAsync() =>
            Task.FromResult<IReadOnlyList<string>>(Directory.Exists(root)
                ? Directory.GetDirectories(root).Select(d => Path.GetFileName(d)).ToList()
                : []);

        public Task<long> LastSeqAsync(string deviceId) => Task.FromResult(Seqs(deviceId).DefaultIfEmpty(0).Max());

        public Task<IReadOnlyList<long>> SeqsAsync(string deviceId) => Task.FromResult<IReadOnlyList<long>>(Seqs(deviceId).Order().ToList());

        public Task DeleteAsync(string deviceId, long seq)
        {
            File.Delete(FileOf(deviceId, seq));
            return Task.CompletedTask;
        }

        private IEnumerable<long> Seqs(string deviceId)
        {
            var folder = Path.Combine(root, deviceId);
            if (!Directory.Exists(folder))
                yield break;
            foreach (var file in Directory.GetFiles(folder, "*" + Extension))
            {
                if (long.TryParse(Path.GetFileNameWithoutExtension(file), NumberStyles.None, CultureInfo.InvariantCulture, out var n)
                    && Path.GetFileName(file) == $"{n:D8}{Extension}")
                    yield return n;
            }
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
        /// <summary>
        /// Copies every file that <paramref name="to"/> lacks from <paramref name="from"/>; returns how many. Files can be missing
        /// in two ways: not delivered yet (a sync tool delivers in any order: copy them when they come) or compacted away (the
        /// device wrote a base file that replaces them: <see cref="SyncEngine"/> skips over such a hole). Neither stops the copy.
        /// </summary>
        public static async Task<int> CopyMissingAsync(ISyncStore from, ISyncStore to)
        {
            var copied = 0;
            foreach (var device in await from.DevicesAsync())
            {
                var have = await to.LastSeqAsync(device);
                IEnumerable<long> candidates;
                if (from is IPrunableSyncStore listable)
                {
                    // Exactly the files that are there; of the other side only what it lacks
                    var present = to is IPrunableSyncStore target ? (await target.SeqsAsync(device)).ToHashSet() : null;
                    candidates = (await listable.SeqsAsync(device)).Where(s => present != null ? !present.Contains(s) : s > have);
                }
                else
                {
                    // A link to another device cannot list, and every probe is a request: walk down from its last file and stop at the first
                    // hole (compacted away: the device keeps its files without gaps from its base on), so the cost is the files that exist
                    var last = await from.LastSeqAsync(device);
                    var present = to is IPrunableSyncStore target ? (await target.SeqsAsync(device)).ToHashSet() : null;
                    candidates = DescendingFiles(last, present != null ? 1 : have + 1, present);
                }

                foreach (var seq in candidates)
                {
                    if (await from.ReadAsync(device, seq) is not { } data)
                    {
                        if (from is IPrunableSyncStore)
                            continue;
                        break; // the hole of the walk down: nothing below it is there
                    }
                    await to.WriteAsync(device, seq, data);
                    copied++;
                }
            }
            return copied;
        }

        private static IEnumerable<long> DescendingFiles(long last, long lowest, HashSet<long>? skip)
        {
            for (var seq = last; seq >= lowest; seq--)
            {
                if (skip == null || !skip.Contains(seq))
                    yield return seq;
            }
        }
    }
}
