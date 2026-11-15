using System.Globalization;
using Android.Content;
using Android.Provider;
using MoneyRecord.Services;
using MoneyRecord.Services.Sync;
using Application = Android.App.Application;
using Uri = Android.Net.Uri;

namespace MoneyRecord.Platforms.Android
{
    /// <summary>
    /// The shared sync folder as a document tree from the system folder picker (Storage Access Framework): Google Drive, Nextcloud,
    /// a Syncthing folder, an SD card, any provider, with no storage permission. The change files live in
    /// <c>&lt;chosen folder&gt;/MoneyRecordSync/&lt;device&gt;/&lt;seq&gt;.chg</c>. The user can take the permission back or delete the
    /// folder: then <see cref="Name"/> is null and <see cref="Open"/> throws (the sync reports "folder unavailable").
    /// </summary>
    public sealed class AndroidSyncFolder(IPreferencesService preferences) : ISyncFolder
    {
        private const string FolderUriKey = "Sync.FolderUri";
        private const string RootName = "MoneyRecordSync";
        private const ActivityFlags ReadWrite = ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission;

        private static ContentResolver Resolver => Application.Context.ContentResolver!;

        public bool IsSupported => true;

        private Uri? Tree => preferences.Get(FolderUriKey, string.Empty) is { Length: > 0 } uri ? Uri.Parse(uri) : null;

        public string? Name
        {
            get
            {
                try
                {
                    if (Tree is not { } tree || !HasPermission(tree))
                        return null;
                    using var cursor = Resolver.Query(DocumentUri(tree, DocumentsContract.GetTreeDocumentId(tree)),
                        [DocumentsContract.Document.ColumnDisplayName], null, null, null);
                    return cursor?.MoveToFirst() == true ? cursor.GetString(0) : null;
                }
                catch (Exception)
                {
                    return null; // deleted, or its provider app was uninstalled
                }
            }
        }

        public async Task<bool> PickAsync()
        {
            if (Platform.CurrentActivity is not MainActivity activity)
                return false;

            var intent = new Intent(Intent.ActionOpenDocumentTree);
            intent.AddFlags(ReadWrite | ActivityFlags.GrantPersistableUriPermission);
            if ((await activity.StartForResultAsync(intent))?.Data is not { } picked)
                return false;

            Resolver.TakePersistableUriPermission(picked, ReadWrite);
            ReleaseOld(picked);
            preferences.Set(FolderUriKey, picked.ToString());
            return true;
        }

        public void Clear()
        {
            ReleaseOld(null);
            preferences.Set(FolderUriKey, string.Empty);
        }

        private void ReleaseOld(Uri? keep)
        {
            // An app may only keep a few persisted permissions: the folder no longer used is given back
            if (Tree is { } previous && (keep == null || !previous.Equals(keep)) && HasPermission(previous))
                Resolver.ReleasePersistableUriPermission(previous, ReadWrite);
        }

        public ISyncStore? Open() =>
            Tree is { } tree
                ? HasPermission(tree) ? new SafSyncStore(tree) : throw new UnauthorizedAccessException("The permission for the sync folder was taken back")
                : null;

        private static bool HasPermission(Uri tree) =>
            Resolver.PersistedUriPermissions.Any(p => tree.Equals(p.Uri) && p.IsWritePermission);

        private static Uri DocumentUri(Uri tree, string documentId) => DocumentsContract.BuildDocumentUriUsingTree(tree, documentId)!;

        /// <summary>
        /// The store over the tree. Listings are cached for the life of the store (one sync run): a provider like Drive is slow to list,
        /// and a run reads each folder once. A file is written under a temporary name and renamed, so a reader never sees half of it.
        /// </summary>
        private sealed class SafSyncStore(Uri tree) : ISyncStore
        {
            private const string Extension = ".chg";
            private const string TempSuffix = ".tmp";

            private record Entry(string Id, string Name, bool IsDirectory);

            private readonly Dictionary<string, List<Entry>> _listings = [];
            private string? _rootId;

            private List<Entry> List(string parentId)
            {
                if (_listings.TryGetValue(parentId, out var cached))
                    return cached;
                var children = DocumentsContract.BuildChildDocumentsUriUsingTree(tree, parentId)!;
                using var cursor = Resolver.Query(children,
                    [DocumentsContract.Document.ColumnDocumentId, DocumentsContract.Document.ColumnDisplayName, DocumentsContract.Document.ColumnMimeType],
                    null, null, null) ?? throw new IOException("The sync folder can't be listed");
                var entries = new List<Entry>();
                while (cursor.MoveToNext())
                {
                    if (cursor.GetString(0) is { } id && cursor.GetString(1) is { } name)
                        entries.Add(new Entry(id, name, cursor.GetString(2) == DocumentsContract.Document.MimeTypeDir));
                }
                return _listings[parentId] = entries;
            }

            /// <summary>The id of the sub-folder <paramref name="name"/> of <paramref name="parentId"/>; null when it is not there and not asked to be made.</summary>
            private string? Folder(string parentId, string name, bool create)
            {
                if (List(parentId).FirstOrDefault(e => e.IsDirectory && e.Name == name) is { } found)
                    return found.Id;
                if (!create)
                    return null;
                var made = DocumentsContract.CreateDocument(Resolver, DocumentUri(tree, parentId), DocumentsContract.Document.MimeTypeDir, name)
                    ?? throw new IOException("The sync folder refused a new folder");
                var id = DocumentsContract.GetDocumentId(made)!;
                _listings.Remove(parentId);
                return id;
            }

            private string? Root(bool create) =>
                _rootId ??= Folder(DocumentsContract.GetTreeDocumentId(tree)!, RootName, create);

            public Task<IReadOnlyList<string>> DevicesAsync() =>
                Task.FromResult<IReadOnlyList<string>>(Root(create: false) is { } root
                    ? List(root).Where(e => e.IsDirectory).Select(e => e.Name).ToList()
                    : []);

            public Task<long> LastSeqAsync(string deviceId)
            {
                long last = 0;
                if (Root(create: false) is { } root && Folder(root, deviceId, create: false) is { } device)
                {
                    foreach (var entry in List(device).Where(e => !e.IsDirectory))
                    {
                        // Only exact names: a sync tool's "00000002 (1).chg" conflict copy is ignored
                        if (entry.Name.EndsWith(Extension, StringComparison.Ordinal)
                            && long.TryParse(entry.Name[..^Extension.Length], NumberStyles.None, CultureInfo.InvariantCulture, out var n)
                            && entry.Name == NameOf(n))
                            last = Math.Max(last, n);
                    }
                }
                return Task.FromResult(last);
            }

            private static string NameOf(long seq) => $"{seq:D8}{Extension}";

            public Task<byte[]?> ReadAsync(string deviceId, long seq)
            {
                if (Root(create: false) is not { } root || Folder(root, deviceId, create: false) is not { } device
                    || List(device).FirstOrDefault(e => !e.IsDirectory && e.Name == NameOf(seq)) is not { } file)
                    return Task.FromResult<byte[]?>(null);
                using var input = Resolver.OpenInputStream(DocumentUri(tree, file.Id)) ?? throw new IOException("The sync folder can't read a file");
                using var memory = new MemoryStream();
                input.CopyTo(memory);
                return Task.FromResult<byte[]?>(memory.ToArray());
            }

            public Task WriteAsync(string deviceId, long seq, byte[] data)
            {
                var root = Root(create: true)!;
                var device = Folder(root, deviceId, create: true)!;
                var existing = List(device).FirstOrDefault(e => !e.IsDirectory && e.Name == NameOf(seq));
                var temp = DocumentsContract.CreateDocument(Resolver, DocumentUri(tree, device), "application/octet-stream", NameOf(seq) + TempSuffix)
                    ?? throw new IOException("The sync folder refused a new file");
                try
                {
                    using (var output = Resolver.OpenOutputStream(temp, "w") ?? throw new IOException("The sync folder can't write a file"))
                        output.Write(data, 0, data.Length);
                    if (existing != null)
                        DocumentsContract.DeleteDocument(Resolver, DocumentUri(tree, existing.Id)); // the same content again: replaced
                    if (DocumentsContract.RenameDocument(Resolver, temp, NameOf(seq)) == null)
                        throw new IOException("The sync folder can't rename a file");
                }
                catch
                {
                    try
                    {
                        DocumentsContract.DeleteDocument(Resolver, temp);
                    }
                    catch (Exception)
                    {
                        // Nothing more to do: a leftover ".tmp" is never read
                    }
                    throw;
                }
                _listings.Remove(device);
                return Task.CompletedTask;
            }
        }
    }
}
