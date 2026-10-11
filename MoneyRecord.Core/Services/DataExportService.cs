using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;
using MoneyRecord.Services.Repositories.Migrations;
using SQLite;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace MoneyRecord.Services
{
    public sealed class DataExportService : IDataExportService
    {
        /// <summary>
        /// v1: decimals only (.json). v2: also exact cents (.json). v3: a .zip with data.json + receipts/,
        /// receipt paths relative to the zip ("receipts/receipt_x.jpg").
        /// </summary>
        public const int BackupVersion = 3;

        private const string DataEntryName = "data.json";
        private const string SnapshotPrefix = "pre-restore-";
        private const int SnapshotsToKeep = 5;

        private readonly DatabaseInitializer _dbInitializer;
        private readonly IAccountRepository _accountRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IAppPaths _paths;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public DataExportService(DatabaseInitializer dbInitializer, IAccountRepository accountRepository, ICategoryRepository categoryRepository, IAppPaths paths)
        {
            _dbInitializer = dbInitializer;
            _accountRepository = accountRepository;
            _categoryRepository = categoryRepository;
            _paths = paths;
        }

        public string BackupsFolder => _paths.BackupsFolder;

        private string ReceiptsFolder => _paths.ReceiptsFolder;

        private async Task EnsureInitializedAsync()
        {
            await _dbInitializer.InitializeAsync();
        }

        // ---- Backup ----

        public async Task ExportBackupAsync(string destinationPath)
        {
            await EnsureInitializedAsync();
            var db = _dbInitializer.Database!;

            // One transaction: a point-in-time copy, even if recurring processing writes meanwhile
            BackupData backup = null!;
            await db.RunInTransactionAsync(conn => backup = new BackupData
            {
                ExportDate = DateTime.Now,
                Version = BackupVersion,
                Categories = conn.Table<Category>().ToList(),
                Accounts = conn.Table<Account>().ToList(),
                Transactions = conn.Table<Transaction>().ToList(),
                Transfers = conn.Table<Transfer>().ToList(),
                Budgets = conn.Table<Budget>().ToList(),
                RecurringTransactions = conn.Table<RecurringTransaction>().ToList()
            });

            await Task.Run(() => WriteZip(backup, destinationPath));
        }

        private void WriteZip(BackupData backup, string destinationPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destinationPath))!);

            // Written next to the destination and moved into place, so a failed export never leaves a broken backup
            var tempPath = destinationPath + ".tmp";
            File.Delete(tempPath);
            try
            {
                using (var zip = ZipFile.Open(tempPath, ZipArchiveMode.Create))
                {
                    var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var transaction in backup.Transactions)
                        transaction.ReceiptPhotoPath = AddReceipt(zip, transaction.ReceiptPhotoPath, added);

                    using var data = zip.CreateEntry(DataEntryName, CompressionLevel.Optimal).Open();
                    JsonSerializer.Serialize(data, backup, JsonOptions);
                }
                File.Move(tempPath, destinationPath, overwrite: true);
            }
            catch
            {
                File.Delete(tempPath);
                throw;
            }
        }

        /// <summary>
        /// Copies a receipt photo into the zip and returns its path inside the zip; null when the file is gone.
        /// </summary>
        private string? AddReceipt(ZipArchive zip, string? path, HashSet<string> added)
        {
            if (!ReceiptFiles.IsInFolder(path, ReceiptsFolder) || !File.Exists(path))
                return null;

            var entryName = $"{ReceiptFiles.FolderName}/{Path.GetFileName(path)}";
            if (added.Add(entryName))
                zip.CreateEntryFromFile(path!, entryName, CompressionLevel.NoCompression); // photos are already compressed
            return entryName;
        }

        // ---- Restore ----

        public async Task<RestoreResult> RestoreAsync(string backupPath)
        {
            await EnsureInitializedAsync();
            var db = _dbInitializer.Database!;

            // Receipts are unpacked next to the live folder and only moved in once the database restore succeeded
            var staging = ReceiptsFolder + ".restoring";
            DeleteDirectory(staging);
            try
            {
                var (backup, hasReceipts) = await Task.Run(() => ReadBackup(backupPath, staging));
                ResolveReceiptPaths(backup, hasReceipts ? staging : ReceiptsFolder);

                // The safety net: no snapshot, no restore
                var snapshotPath = Path.Combine(BackupsFolder, $"{SnapshotPrefix}{DateTime.Now:yyyyMMdd_HHmmss_fff}.zip");
                await ExportBackupAsync(snapshotPath);

                var count = 0;
                await db.RunInTransactionAsync(conn => count = ReplaceAll(conn, backup));
                _accountRepository.InvalidateCache();
                _categoryRepository.InvalidateCache();

                // The data is restored: from here on it's cleanup, which must not turn the restore into a failure
                try
                {
                    await Task.Run(() => ReplaceReceipts(hasReceipts ? staging : null, backup));
                    PruneSnapshots();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Some photos may be missing; the snapshot still has the old ones and allows undo
                }

                return new RestoreResult(count, snapshotPath);
            }
            finally
            {
                DeleteDirectory(staging);
            }
        }

        /// <summary>
        /// Reads and validates the whole backup before anything is changed. A .zip's receipts are unpacked into
        /// <paramref name="staging"/>; an old .json backup has none.
        /// </summary>
        private static (BackupData Backup, bool HasReceipts) ReadBackup(string backupPath, string staging)
        {
            using var file = File.OpenRead(backupPath);
            var isZip = file.ReadByte() == 'P' && file.ReadByte() == 'K';
            file.Position = 0;

            if (!isZip)
                return (Deserialize(file), false);

            using var zip = new ZipArchive(file, ZipArchiveMode.Read);
            var dataEntry = zip.GetEntry(DataEntryName)
                ?? throw new InvalidDataException("Not a MoneyRecord backup: data.json is missing");

            BackupData backup;
            using (var data = dataEntry.Open())
                backup = Deserialize(data);

            Directory.CreateDirectory(staging);
            foreach (var entry in zip.Entries)
            {
                if (!entry.FullName.StartsWith(ReceiptFiles.FolderName + "/", StringComparison.Ordinal))
                    continue;

                // Only plain file names: an entry can never write outside the staging folder
                var name = entry.Name;
                if (name.Length == 0 || name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    continue;

                entry.ExtractToFile(Path.Combine(staging, name), overwrite: true);
            }

            return (backup, true);
        }

        private static BackupData Deserialize(Stream json)
        {
            var backup = JsonSerializer.Deserialize<BackupData>(json, JsonOptions)
                ?? throw new InvalidDataException("Invalid backup file");

            if (backup.Version > BackupVersion)
                throw new InvalidDataException($"The backup was made by a newer version of the app (format v{backup.Version})");

            EnsureUniqueIds(backup.Categories, c => c.Id);
            EnsureUniqueIds(backup.Accounts, a => a.Id);
            EnsureUniqueIds(backup.Transactions, t => t.Id);
            EnsureUniqueIds(backup.Transfers, t => t.Id);
            EnsureUniqueIds(backup.Budgets, b => b.Id);
            EnsureUniqueIds(backup.RecurringTransactions, r => r.Id);
            return backup;
        }

        private static void EnsureUniqueIds<T>(List<T> rows, Func<T, int> id)
        {
            var ids = rows.Select(id).Where(i => i != 0).ToList();
            if (ids.Count != ids.Distinct().Count())
                throw new InvalidDataException($"Invalid backup file: duplicate {typeof(T).Name} IDs");
        }

        /// <summary>
        /// Points each receipt at the live receipts folder by file name ("receipts/x.jpg" in a zip, or an absolute
        /// path from an old .json backup). A photo that won't be there after the restore is dropped.
        /// </summary>
        private void ResolveReceiptPaths(BackupData backup, string availableReceipts)
        {
            foreach (var transaction in backup.Transactions)
            {
                var name = Path.GetFileName(transaction.ReceiptPhotoPath);
                transaction.ReceiptPhotoPath = !string.IsNullOrWhiteSpace(name) && File.Exists(Path.Combine(availableReceipts, name))
                    ? Path.Combine(ReceiptsFolder, name)
                    : null;
            }
        }

        /// <summary>
        /// Deletes everything and inserts the backup, keeping every row's ID so that all references
        /// (category, account, transfer) stay exactly as they were. Runs inside the caller's transaction.
        /// </summary>
        private static int ReplaceAll(SQLiteConnection conn, BackupData backup)
        {
            conn.DeleteAll<RecurringTransaction>();
            conn.DeleteAll<Budget>();
            conn.DeleteAll<Transfer>();
            conn.DeleteAll<Transaction>();
            conn.DeleteAll<Account>();
            conn.DeleteAll<Category>();

            var count = InsertKeepingIds(conn, backup.Categories, c => c.Id)
                + InsertKeepingIds(conn, backup.Accounts, a => a.Id)
                + InsertKeepingIds(conn, backup.Transactions, t => t.Id)
                + InsertKeepingIds(conn, backup.Transfers, t => t.Id)
                + InsertKeepingIds(conn, backup.Budgets, b => b.Id)
                + InsertKeepingIds(conn, backup.RecurringTransactions, r => r.Id);

            // A backup from before account currencies: give its accounts a fixed currency, as migration 3 does
            M003_AccountCurrencyCodes.FillMissing(conn, MoneyFormatter.ResolveCurrencyCode(null));
            return count;
        }

        private static int InsertKeepingIds<T>(SQLiteConnection conn, List<T> rows, Func<T, int> id) where T : notnull
        {
            // sqlite-net only writes an AutoIncrement key with "OR REPLACE" (the table is empty, so nothing is replaced).
            // Rows without an ID (very old backups) go last and get a new one.
            foreach (var row in rows.Where(r => id(r) != 0))
                conn.Insert(row, "OR REPLACE");
            foreach (var row in rows.Where(r => id(r) == 0))
                conn.Insert(row);
            return rows.Count;
        }

        /// <summary>
        /// Moves in the zip's receipts (if any) and leaves only the photos the restored transactions point at.
        /// The old photos are not lost: they are in the pre-restore snapshot.
        /// </summary>
        private void ReplaceReceipts(string? staging, BackupData backup)
        {
            var referenced = backup.Transactions
                .Select(t => Path.GetFileName(t.ReceiptPhotoPath))
                .Where(name => !string.IsNullOrEmpty(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            Directory.CreateDirectory(ReceiptsFolder);
            if (staging != null)
            {
                foreach (var file in Directory.GetFiles(staging).Where(f => referenced.Contains(Path.GetFileName(f))))
                    File.Move(file, Path.Combine(ReceiptsFolder, Path.GetFileName(file)), overwrite: true);
            }

            foreach (var file in Directory.GetFiles(ReceiptsFolder))
            {
                if (!referenced.Contains(Path.GetFileName(file)))
                    ReceiptFiles.Delete(file, ReceiptsFolder);
            }
        }

        private void PruneSnapshots()
        {
            var old = Directory.GetFiles(BackupsFolder, SnapshotPrefix + "*.zip")
                .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
                .Skip(SnapshotsToKeep);
            foreach (var file in old)
            {
                try { File.Delete(file); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }

        private static void DeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        // ---- CSV ----

        public async Task<string> ExportCsvAsync()
        {
            await EnsureInitializedAsync();
            var db = _dbInitializer.Database!;

            var transactions = await db.Table<Transaction>().ToListAsync();
            var categories = (await db.Table<Category>().ToListAsync()).ToDictionary(c => c.Id);
            var accounts = (await db.Table<Account>().ToListAsync()).ToDictionary(a => a.Id);

            var sb = new StringBuilder();
            sb.AppendLine("Date,Description,Amount,Type,Category,Account");

            foreach (var t in transactions.OrderByDescending(t => t.Date))
            {
                var categoryName = categories.TryGetValue(t.CategoryId, out var cat) ? cat.Name : "";
                var accountName = t.AccountId.HasValue && accounts.TryGetValue(t.AccountId.Value, out var acc) ? acc.Name : "";

                sb.Append(t.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.Append(CsvEscape(t.Description));
                sb.Append(',');
                sb.Append(t.Amount.ToString("F2", CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.Append(t.Type);
                sb.Append(',');
                sb.Append(CsvEscape(categoryName));
                sb.Append(',');
                sb.AppendLine(CsvEscape(accountName));
            }

            return sb.ToString();
        }

        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }

    internal class BackupData
    {
        public DateTime ExportDate { get; set; }
        public int Version { get; set; }
        public List<Category> Categories { get; set; } = [];
        public List<Account> Accounts { get; set; } = [];
        public List<Transaction> Transactions { get; set; } = [];
        public List<Transfer> Transfers { get; set; } = [];
        public List<Budget> Budgets { get; set; } = [];
        public List<RecurringTransaction> RecurringTransactions { get; set; } = [];
    }
}
