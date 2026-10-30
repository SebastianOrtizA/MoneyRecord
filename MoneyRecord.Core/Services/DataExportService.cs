using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;
using MoneyRecord.Services.Repositories.Migrations;
using SQLite;
using System.Globalization;
using System.Security.Cryptography;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Zip.Compression;
using System.Text;
using System.Text.Json;

namespace MoneyRecord.Services
{
    public sealed class DataExportService : IDataExportService
    {
        /// <summary>
        /// v1: decimals only (.json). v2: also exact cents (.json). v3: a .zip with data.json + receipts/,
        /// receipt paths relative to the zip ("receipts/receipt_x.jpg"). v4: transfers also store the destination amount;
        /// savings goals and their accounts, payees, debts, trips (added before v4 shipped, so an app that can read v4 knows them).
        /// v5: parties (friends, parties with members, expenses, exclusions, contributions, settle-up payments).
        /// v6: pockets, their movements and their goal links. v7: savings challenges and their days. v8: a linear challenge's first day.
        /// </summary>
        public const int BackupVersion = 8;

        private const string DataEntryName = "data.json";
        private const string SnapshotPrefix = "pre-restore-";
        private const int SnapshotsToKeep = 5;

        private readonly DatabaseInitializer _dbInitializer;
        private readonly IAccountRepository _accountRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ITransferRepository _transferRepository;
        private readonly IPayeeRepository _payeeRepository;
        private readonly IAppPaths _paths;
        private readonly IBackupHistory _backupHistory;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public DataExportService(DatabaseInitializer dbInitializer, IAccountRepository accountRepository, ICategoryRepository categoryRepository, ITransactionRepository transactionRepository, ITransferRepository transferRepository, IPayeeRepository payeeRepository, IAppPaths paths, IBackupHistory backupHistory)
        {
            _backupHistory = backupHistory;
            _dbInitializer = dbInitializer;
            _accountRepository = accountRepository;
            _categoryRepository = categoryRepository;
            _transactionRepository = transactionRepository;
            _transferRepository = transferRepository;
            _payeeRepository = payeeRepository;
            _paths = paths;
        }

        public string BackupsFolder => _paths.BackupsFolder;

        private string ReceiptsFolder => _paths.ReceiptsFolder;

        // ---- Backup ----

        public async Task<string> ExportBackupAsync(string destinationPath, string? password = null)
        {
            var backup = await ReadDataAsync();
            // Before WriteZip, which rewrites the receipt paths
            var fingerprint = Fingerprint(backup);
            await Task.Run(() => WriteZip(backup, destinationPath, password));
            _backupHistory.MarkWritten();
            return fingerprint;
        }

        public async Task<string> GetFingerprintAsync() => Fingerprint(await ReadDataAsync());

        /// <summary>A hash of the rows (not the export time): equal fingerprints = a backup would hold the same data.</summary>
        private static string Fingerprint(BackupData backup) =>
            Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(backup with { ExportDate = default })));

        private async Task<BackupData> ReadDataAsync()
        {
            // One transaction: a point-in-time copy, even if recurring processing writes meanwhile
            BackupData backup = null!;
            await _dbInitializer.RunInTransactionAsync(conn => backup = new BackupData
            {
                ExportDate = DateTime.Now,
                Version = BackupVersion,
                Categories = conn.Table<Category>().ToList(),
                Accounts = conn.Table<Account>().ToList(),
                Transactions = conn.Table<Transaction>().ToList(),
                Transfers = conn.Table<Transfer>().ToList(),
                Budgets = conn.Table<Budget>().ToList(),
                RecurringTransactions = conn.Table<RecurringTransaction>().ToList(),
                SavingsGoals = conn.Table<SavingsGoal>().ToList(),
                SavingsGoalAccounts = conn.Table<SavingsGoalAccount>().ToList(),
                Payees = conn.Table<Payee>().ToList(),
                Debts = conn.Table<Debt>().ToList(),
                Trips = conn.Table<Trip>().ToList(),
                TripCategories = conn.Table<TripCategory>().ToList(),
                TripBudgets = conn.Table<TripBudget>().ToList(),
                Participants = conn.Table<Participant>().ToList(),
                Parties = conn.Table<PartyEvent>().ToList(),
                PartyMembers = conn.Table<PartyMember>().ToList(),
                PartyExpenses = conn.Table<PartyExpense>().ToList(),
                PartyExpenseExclusions = conn.Table<PartyExpenseExclusion>().ToList(),
                PartyContributions = conn.Table<PartyContribution>().ToList(),
                PartyPayments = conn.Table<PartyPayment>().ToList(),
                Pockets = conn.Table<Pocket>().ToList(),
                PocketMovements = conn.Table<PocketMovement>().ToList(),
                SavingsGoalPockets = conn.Table<SavingsGoalPocket>().ToList(),
                SavingsChallenges = conn.Table<SavingsChallenge>().ToList(),
                SavingsChallengeDays = conn.Table<SavingsChallengeDay>().ToList()
            });
            return backup;
        }

        /// <summary>
        /// A standard zip; with a password every entry is AES-256 encrypted (7-Zip and other zip tools open it too).
        /// </summary>
        private void WriteZip(BackupData backup, string destinationPath, string? password)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destinationPath))!);

            // Written next to the destination and moved into place, so a failed export never leaves a broken backup
            var tempPath = destinationPath + ".tmp";
            File.Delete(tempPath);
            try
            {
                using (var zip = new ZipOutputStream(File.Create(tempPath)))
                {
                    if (!string.IsNullOrEmpty(password))
                        zip.Password = password;

                    var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var transaction in backup.Transactions)
                        transaction.ReceiptPhotoPath = AddReceipt(zip, transaction.ReceiptPhotoPath, added);

                    PutEntry(zip, DataEntryName, output => JsonSerializer.Serialize(output, backup, JsonOptions));
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
        private string? AddReceipt(ZipOutputStream zip, string? path, HashSet<string> added)
        {
            if (!ReceiptFiles.IsInFolder(path, ReceiptsFolder) || !File.Exists(path))
                return null;

            var entryName = $"{ReceiptFiles.FolderName}/{Path.GetFileName(path)}";
            if (added.Add(entryName))
            {
                // Photos are already compressed: no CPU spent deflating them again
                PutEntry(zip, entryName, output =>
                {
                    using var photo = File.OpenRead(path!);
                    photo.CopyTo(output);
                }, compress: false);
            }
            return entryName;
        }

        private static void PutEntry(ZipOutputStream zip, string name, Action<Stream> write, bool compress = true)
        {
            // Level 0 writes stored blocks: one code path for every entry, plain or encrypted
            zip.SetLevel(compress ? Deflater.DEFAULT_COMPRESSION : Deflater.NO_COMPRESSION);
            var entry = new ZipEntry(name) { DateTime = DateTime.Now };
            if (zip.Password != null)
                entry.AESKeySize = 256;
            zip.PutNextEntry(entry);
            write(zip);
            zip.CloseEntry();
        }

        // ---- Restore ----

        public async Task<RestoreResult> RestoreAsync(string backupPath, string? password = null)
        {
            // Receipts are unpacked next to the live folder and only moved in once the database restore succeeded
            var staging = ReceiptsFolder + ".restoring";
            DeleteDirectory(staging);
            try
            {
                var (backup, hasReceipts) = await Task.Run(() => ReadBackup(backupPath, staging, password));
                ResolveReceiptPaths(backup, hasReceipts ? staging : ReceiptsFolder);

                // The safety net: no snapshot, no restore
                var snapshotPath = Path.Combine(BackupsFolder, $"{SnapshotPrefix}{DateTime.Now:yyyyMMdd_HHmmss_fff}.zip");
                await ExportBackupAsync(snapshotPath);

                var count = 0;
                await _dbInitializer.RunInTransactionAsync(conn => count = ReplaceAll(conn, backup));
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
        /// <paramref name="staging"/>; an old .json backup has none. An encrypted zip needs its password
        /// (<see cref="BackupPasswordException"/> otherwise).
        /// </summary>
        private static (BackupData Backup, bool HasReceipts) ReadBackup(string backupPath, string staging, string? password)
        {
            using var file = File.OpenRead(backupPath);
            var isZip = file.ReadByte() == 'P' && file.ReadByte() == 'K';
            file.Position = 0;

            if (!isZip)
                return (Deserialize(file), false);

            using var zip = new ZipFile(file, leaveOpen: true);
            var dataEntry = zip.GetEntry(DataEntryName)
                ?? throw new InvalidDataException("Not a MoneyRecord backup: data.json is missing");
            if (dataEntry.IsCrypted)
            {
                if (string.IsNullOrEmpty(password))
                    throw new BackupPasswordException(wrongPassword: false);
                zip.Password = password;
            }

            BackupData backup;
            using (var data = OpenEntry(zip, dataEntry))
                backup = Deserialize(data);

            Directory.CreateDirectory(staging);
            const string receiptsPrefix = ReceiptFiles.FolderName + "/";
            foreach (ZipEntry entry in zip)
            {
                if (!entry.IsFile || !entry.Name.StartsWith(receiptsPrefix, StringComparison.Ordinal))
                    continue;

                // Only plain file names: an entry can never write outside the staging folder
                var name = entry.Name[receiptsPrefix.Length..];
                if (name.Length == 0 || name is "." or ".." || name.Contains('/')
                    || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    continue;

                using var input = OpenEntry(zip, entry);
                using var output = File.Create(Path.Combine(staging, name));
                input.CopyTo(output);
            }

            return (backup, true);
        }

        /// <summary>A wrong password shows when an encrypted entry is opened (AES keeps a password check value).</summary>
        private static Stream OpenEntry(ZipFile zip, ZipEntry entry)
        {
            try
            {
                return zip.GetInputStream(entry);
            }
            catch (ZipException) when (entry.IsCrypted)
            {
                throw new BackupPasswordException(wrongPassword: true);
            }
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
            EnsureUniqueIds(backup.SavingsGoals, g => g.Id);
            EnsureUniqueIds(backup.SavingsGoalAccounts, l => l.Id);
            EnsureUniqueIds(backup.Payees, p => p.Id);
            EnsureUniqueIds(backup.Debts, d => d.Id);
            EnsureUniqueIds(backup.Trips, t => t.Id);
            EnsureUniqueIds(backup.TripCategories, c => c.Id);
            EnsureUniqueIds(backup.TripBudgets, b => b.Id);
            EnsureUniqueIds(backup.Participants, p => p.Id);
            EnsureUniqueIds(backup.Parties, p => p.Id);
            EnsureUniqueIds(backup.PartyMembers, m => m.Id);
            EnsureUniqueIds(backup.PartyExpenses, e => e.Id);
            EnsureUniqueIds(backup.PartyExpenseExclusions, x => x.Id);
            EnsureUniqueIds(backup.PartyContributions, c => c.Id);
            EnsureUniqueIds(backup.PartyPayments, p => p.Id);
            EnsureUniqueIds(backup.Pockets, p => p.Id);
            EnsureUniqueIds(backup.PocketMovements, m => m.Id);
            EnsureUniqueIds(backup.SavingsGoalPockets, l => l.Id);
            EnsureUniqueIds(backup.SavingsChallenges, c => c.Id);
            EnsureUniqueIds(backup.SavingsChallengeDays, d => d.Id);
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
            conn.DeleteAll<PartyPayment>();
            conn.DeleteAll<PartyContribution>();
            conn.DeleteAll<PartyExpenseExclusion>();
            conn.DeleteAll<PartyExpense>();
            conn.DeleteAll<PartyMember>();
            conn.DeleteAll<PartyEvent>();
            conn.DeleteAll<Participant>();
            conn.DeleteAll<TripBudget>();
            conn.DeleteAll<TripCategory>();
            conn.DeleteAll<Trip>();
            conn.DeleteAll<Debt>();
            conn.DeleteAll<Payee>();
            conn.DeleteAll<SavingsChallengeDay>();
            conn.DeleteAll<SavingsChallenge>();
            conn.DeleteAll<SavingsGoalPocket>();
            conn.DeleteAll<PocketMovement>();
            conn.DeleteAll<Pocket>();
            conn.DeleteAll<SavingsGoalAccount>();
            conn.DeleteAll<SavingsGoal>();
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
                + InsertKeepingIds(conn, backup.RecurringTransactions, r => r.Id)
                + InsertKeepingIds(conn, backup.SavingsGoals, g => g.Id)
                + InsertKeepingIds(conn, backup.Payees, p => p.Id)
                + InsertKeepingIds(conn, backup.Debts, d => d.Id)
                + InsertKeepingIds(conn, backup.Trips, t => t.Id)
                + InsertKeepingIds(conn, backup.TripCategories, c => c.Id)
                + InsertKeepingIds(conn, backup.TripBudgets, b => b.Id)
                + InsertKeepingIds(conn, backup.Participants, p => p.Id)
                + InsertKeepingIds(conn, backup.Parties, p => p.Id)
                + InsertKeepingIds(conn, backup.PartyMembers, m => m.Id)
                + InsertKeepingIds(conn, backup.PartyExpenses, e => e.Id)
                + InsertKeepingIds(conn, backup.PartyExpenseExclusions, x => x.Id)
                + InsertKeepingIds(conn, backup.PartyContributions, c => c.Id)
                + InsertKeepingIds(conn, backup.PartyPayments, p => p.Id)
                + InsertKeepingIds(conn, backup.Pockets, p => p.Id)
                + InsertKeepingIds(conn, backup.PocketMovements, m => m.Id)
                + InsertKeepingIds(conn, backup.SavingsChallenges, c => c.Id)
                + InsertKeepingIds(conn, backup.SavingsChallengeDays, d => d.Id);
            // Links, not items of their own
            InsertKeepingIds(conn, backup.SavingsGoalAccounts, l => l.Id);
            InsertKeepingIds(conn, backup.SavingsGoalPockets, l => l.Id);

            // A backup from before trips has no trip categories: start with the defaults, as a new database does
            if (backup.TripCategories.Count == 0)
                DatabaseInitializer.SeedTripCategories(conn);

            // A backup from before account currencies: give its accounts a fixed currency, as migration 3 does
            M003_AccountCurrencyCodes.FillMissing(conn, MoneyFormatter.BaseCurrency);
            // A backup from before destination amounts (v3 and older): what arrived is what left, as migration 4 does
            M004_TransferDestinationAmounts.FillMissing(conn);
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

        public async Task<string> ExportCsvAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var (start, end) = (startDate ?? DateTime.MinValue, endDate ?? DateTime.MaxValue);
            var allTime = startDate == null && endDate == null;
            var transactions = allTime ? await _transactionRepository.GetAllAsync() : await _transactionRepository.GetByDateRangeAsync(start, end);
            var transfers = allTime ? await _transferRepository.GetAllAsync() : await _transferRepository.GetByDateRangeAsync(start, end);
            var categories = (await _categoryRepository.GetAllAsync()).ToDictionary(c => c.Id, c => c.Name);
            var accounts = (await _accountRepository.GetAllAsync()).ToDictionary(a => a.Id);
            var payees = (await _payeeRepository.GetAllAsync()).ToDictionary(p => p.Id, p => p.Name);
            var db = await _dbInitializer.GetConnectionAsync();
            var trips = (await db.Table<Trip>().ToListAsync()).ToDictionary(t => t.Id, t => t.Name);
            var tripCategories = (await db.Table<TripCategory>().ToListAsync()).ToDictionary(c => c.Id, c => c.Name);

            string AccountName(int? id) => id is int accountId && accounts.TryGetValue(accountId, out var account) ? account.Name : string.Empty;
            // No account (or an unknown one) = the base currency, like everywhere else
            string Currency(int? id) => MoneyFormatter.ResolveCurrencyCode(
                id is int accountId && accounts.TryGetValue(accountId, out var account) ? account.CurrencyCode : null);
            string TripName(int? id) => id is int tripId ? trips.GetValueOrDefault(tripId, string.Empty) : string.Empty;
            static string Amount(decimal amount) => Math.Abs(amount).ToString("F2", CultureInfo.InvariantCulture);

            var rows = transactions.Select(t => (t.Date, Fields: new[]
                {
                    t.Description, Amount(t.Amount), Currency(t.AccountId), t.Type.ToString(),
                    categories.GetValueOrDefault(t.CategoryId, string.Empty), AccountName(t.AccountId),
                    t.PayeeId is int payeeId ? payees.GetValueOrDefault(payeeId, string.Empty) : string.Empty,
                    string.Empty, string.Empty, string.Empty, t.SplitId ?? string.Empty,
                    TripName(t.TripId), t.TripCategoryId is int tripCategoryId ? tripCategories.GetValueOrDefault(tripCategoryId, string.Empty) : string.Empty
                }))
                .Concat(transfers.Select(t => (t.Date, Fields: new[]
                {
                    t.Description, Amount(t.Amount), Currency(t.SourceAccountId), "Transfer", string.Empty,
                    AccountName(t.SourceAccountId), string.Empty, AccountName(t.DestinationAccountId),
                    Amount(t.DestinationAmount), Currency(t.DestinationAccountId), string.Empty, string.Empty, string.Empty
                })));

            var sb = new StringBuilder();
            sb.AppendLine("Date,Description,Amount,Currency,Type,Category,Account,Payee,ToAccount,ToAmount,ToCurrency,SplitId,Trip,TripCategory");
            foreach (var (date, fields) in rows.OrderByDescending(r => r.Date))
            {
                sb.Append(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                foreach (var field in fields)
                    sb.Append(',').Append(CsvEscape(field));
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.AsSpan().IndexOfAny(",\"\r\n") >= 0)
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }

    internal record BackupData
    {
        public DateTime ExportDate { get; set; }
        public int Version { get; set; }
        public List<Category> Categories { get; set; } = [];
        public List<Account> Accounts { get; set; } = [];
        public List<Transaction> Transactions { get; set; } = [];
        public List<Transfer> Transfers { get; set; } = [];
        public List<Budget> Budgets { get; set; } = [];
        public List<RecurringTransaction> RecurringTransactions { get; set; } = [];
        public List<SavingsGoal> SavingsGoals { get; set; } = [];
        public List<SavingsGoalAccount> SavingsGoalAccounts { get; set; } = [];
        public List<Payee> Payees { get; set; } = [];
        public List<Debt> Debts { get; set; } = [];
        public List<Trip> Trips { get; set; } = [];
        public List<TripCategory> TripCategories { get; set; } = [];
        public List<TripBudget> TripBudgets { get; set; } = [];
        public List<Participant> Participants { get; set; } = [];
        public List<PartyEvent> Parties { get; set; } = [];
        public List<PartyMember> PartyMembers { get; set; } = [];
        public List<PartyExpense> PartyExpenses { get; set; } = [];
        public List<PartyExpenseExclusion> PartyExpenseExclusions { get; set; } = [];
        public List<PartyContribution> PartyContributions { get; set; } = [];
        public List<PartyPayment> PartyPayments { get; set; } = [];
        public List<Pocket> Pockets { get; set; } = [];
        public List<PocketMovement> PocketMovements { get; set; } = [];
        public List<SavingsGoalPocket> SavingsGoalPockets { get; set; } = [];
        public List<SavingsChallenge> SavingsChallenges { get; set; } = [];
        public List<SavingsChallengeDay> SavingsChallengeDays { get; set; } = [];
    }
}
