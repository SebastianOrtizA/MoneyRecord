using Android.Content;
using Android.Provider;
using Microsoft.Extensions.Logging;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using Application = Android.App.Application;
using Uri = Android.Net.Uri;

namespace MoneyRecord.Platforms.Android
{
    /// <summary>
    /// The backup folder as a document tree from the system folder picker (Storage Access Framework): any provider works
    /// (the phone's storage, an SD card, Google Drive, Nextcloud, ...) and no storage permission is needed. The app keeps
    /// a persisted permission for the tree; the user can revoke it, or the folder can disappear, and then
    /// <see cref="Name"/> is null.
    /// </summary>
    public sealed class AndroidBackupFolder(IPreferencesService preferences, ILogger<AndroidBackupFolder> logger) : IBackupFolder
    {
        private const string FolderUriKey = "AutoBackup.FolderUri";
        private const ActivityFlags ReadWrite = ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission;

        private static ContentResolver Resolver => Application.Context.ContentResolver!;

        public bool IsSupported => true;

        private Uri? Tree => preferences.Get(FolderUriKey, string.Empty) is { Length: > 0 } uri ? Uri.Parse(uri) : null;

        public string? Name
        {
            get
            {
                if (Tree is not { } tree || !HasPermission(tree))
                    return null;
                try
                {
                    using var cursor = Resolver.Query(FolderDocument(tree), [DocumentsContract.Document.ColumnDisplayName],
                        null, null, null);
                    return cursor?.MoveToFirst() == true ? cursor.GetString(0) : null;
                }
                catch (Exception ex)
                {
                    // The folder was deleted, or its provider (an app) was uninstalled
                    logger.LogWarning(ex, "The backup folder can't be reached");
                    return null;
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

            // Kept after a restart; the previous folder's permission is given back (an app may only keep a few)
            Resolver.TakePersistableUriPermission(picked, ReadWrite);
            if (Tree is { } previous && !previous.Equals(picked) && HasPermission(previous))
                Resolver.ReleasePersistableUriPermission(previous, ReadWrite);
            preferences.Set(FolderUriKey, picked.ToString());
            return true;
        }

        public IReadOnlyList<string> ListFiles() => Children().Select(c => c.Name).ToList();

        public void Write(string fileName, string sourcePath)
        {
            var document = DocumentsContract.CreateDocument(Resolver, FolderDocument(RequireTree()), "application/zip", fileName)
                ?? throw new IOException("The backup folder refused the new file");
            try
            {
                using var output = Resolver.OpenOutputStream(document, "w")
                    ?? throw new IOException("The backup folder can't write the new file");
                using var input = File.OpenRead(sourcePath);
                input.CopyTo(output);
            }
            catch
            {
                // No half-written backup left in the folder
                try
                {
                    DocumentsContract.DeleteDocument(Resolver, document);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete an unfinished backup");
                }
                throw;
            }
        }

        public void Delete(string fileName)
        {
            var tree = RequireTree();
            foreach (var child in Children().Where(c => c.Name == fileName))
                DocumentsContract.DeleteDocument(Resolver, DocumentsContract.BuildDocumentUriUsingTree(tree, child.Id)!);
        }

        /// <summary>The files (not folders) directly in the tree.</summary>
        private List<(string Id, string Name)> Children()
        {
            var tree = RequireTree();
            var children = DocumentsContract.BuildChildDocumentsUriUsingTree(tree, DocumentsContract.GetTreeDocumentId(tree))!;
            using var cursor = Resolver.Query(children,
                [DocumentsContract.Document.ColumnDocumentId, DocumentsContract.Document.ColumnDisplayName,
                    DocumentsContract.Document.ColumnMimeType], null, null, null)
                ?? throw new IOException("The backup folder can't be listed");

            var files = new List<(string, string)>();
            while (cursor.MoveToNext())
            {
                if (cursor.GetString(2) != DocumentsContract.Document.MimeTypeDir
                    && cursor.GetString(0) is { } id && cursor.GetString(1) is { } name)
                    files.Add((id, name));
            }
            return files;
        }

        private Uri RequireTree() => Tree ?? throw new InvalidOperationException("No backup folder was chosen");

        private static Uri FolderDocument(Uri tree) =>
            DocumentsContract.BuildDocumentUriUsingTree(tree, DocumentsContract.GetTreeDocumentId(tree))!;

        private static bool HasPermission(Uri tree) =>
            Resolver.PersistedUriPermissions.Any(p => tree.Equals(p.Uri) && p.IsWritePermission);
    }
}
