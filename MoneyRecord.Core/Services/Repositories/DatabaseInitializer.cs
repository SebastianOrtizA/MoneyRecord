using System.Security.Cryptography;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories.Migrations;
using SQLite;

namespace MoneyRecord.Services.Repositories
{
    /// <summary>
    /// The app database: opens it on first use (creating the schema, running migrations and seeding default data)
    /// and hands out the connection. All access goes through <see cref="GetConnectionAsync"/> or
    /// <see cref="RunInTransactionAsync"/>, so nothing can use the database before it is ready.
    /// <para>
    /// It is also the optional encryption: the database is either <see cref="IAppPaths.DatabasePath"/> (plain) or
    /// <see cref="IAppPaths.EncryptedDatabasePath"/> (encrypted with the key in <see cref="IDatabaseKeyStore"/>).
    /// </para>
    /// </summary>
    public sealed class DatabaseInitializer : IDatabaseEncryption
    {
        /// <summary>
        /// An encrypted database whose key is gone gets this suffix plus the time: it can't be read, but isn't deleted.
        /// </summary>
        public const string UnreadableSuffix = ".unreadable";

        private const string TempSuffix = ".tmp";
        private const SQLiteOpenFlags OpenFlags = SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex;

        private readonly string _plainPath;
        private readonly string _encryptedPath;
        private readonly IDatabaseKeyStore _keyStore;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private SQLiteAsyncConnection? _database;
        private bool _unreadableNotice;

        static DatabaseInitializer() => SqliteSetup.Init();

        /// <param name="keyStore">None (the tests that don't encrypt): the database can't be encrypted.</param>
        public DatabaseInitializer(IAppPaths paths, IDatabaseKeyStore? keyStore = null)
        {
            _plainPath = paths.DatabasePath;
            _encryptedPath = paths.EncryptedDatabasePath;
            _keyStore = keyStore ?? NoDatabaseKeyStore.Instance;
        }

        /// <summary>The open connection, initializing the database on the first call.</summary>
        public async Task<SQLiteAsyncConnection> GetConnectionAsync()
        {
            if (_database != null)
                return _database;

            await _initLock.WaitAsync();
            try
            {
                if (_database != null)
                    return _database;

                var db = await OpenAsync();
                await CreateSchemaAsync(db, new MigrationRunner(MigrationRunner.All));
                await SeedDefaultDataAsync(db);

                // Published only when ready: a concurrent caller never sees a half-initialized database
                _database = db;
                return db;
            }
            finally
            {
                _initLock.Release();
            }
        }

        private async Task<SQLiteAsyncConnection> OpenAsync()
        {
            if (File.Exists(_encryptedPath))
            {
                var key = await _keyStore.GetAsync();
                if (key != null && await Task.Run(() => CanOpen(_encryptedPath, key)))
                {
                    // A switch that stopped after the encrypted file was complete: the plain one is a leftover
                    DeleteFile(_plainPath);
                    return new SQLiteAsyncConnection(ConnectionString(_encryptedPath, key));
                }

                // The key is gone (app data partly cleared): nothing can read this file again. It is kept aside (a
                // name per time, so an earlier one stays too), and the app goes on with the plain database if there
                // is one, or a new one.
                File.Move(_encryptedPath, $"{_encryptedPath}{UnreadableSuffix}-{DateTime.Now:yyyyMMdd-HHmmss}", overwrite: true);
                _unreadableNotice = true;
            }
            return new SQLiteAsyncConnection(ConnectionString(_plainPath, key: null));
        }

        // ---- Encryption ----

        public bool IsSupported => _keyStore.IsSupported;

        public bool IsEncrypted => File.Exists(_encryptedPath);

        public bool TakeUnreadableDatabaseNotice()
        {
            var notice = _unreadableNotice;
            _unreadableNotice = false;
            return notice;
        }

        public Task EncryptAsync() => SwitchFileAsync(async () =>
        {
            if (IsEncrypted)
                return;

            // 256 random bits as a hex passphrase (the cipher still runs its key derivation on every open)
            var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var temp = _encryptedPath + TempSuffix;
            await Task.Run(() => Rekey(_plainPath, temp, fromKey: null, toKey: key));

            // Saved before the encrypted file exists: there is never an encrypted database without its key
            try
            {
                await _keyStore.SetAsync(key);
            }
            catch
            {
                DeleteFile(temp);
                throw;
            }
            File.Move(temp, _encryptedPath, overwrite: true);
            DeleteFile(_plainPath);
            DeleteMigrationCopies(_plainPath); // older plain copies of the data
        });

        public Task DecryptAsync() => SwitchFileAsync(async () =>
        {
            if (!IsEncrypted)
                return;

            var key = await _keyStore.GetAsync()
                ?? throw new InvalidOperationException("The database key is missing.");
            var temp = _plainPath + TempSuffix;
            await Task.Run(() => Rekey(_encryptedPath, temp, fromKey: key, toKey: null));

            File.Move(temp, _plainPath, overwrite: true);
            DeleteFile(_encryptedPath);
            DeleteMigrationCopies(_encryptedPath); // unreadable once the key is gone
            _keyStore.Remove();
        });

        /// <summary>Closes the connection for <paramref name="work"/>; the next caller opens whichever file it left.</summary>
        private async Task SwitchFileAsync(Func<Task> work)
        {
            await GetConnectionAsync(); // an unopened database may still need its migrations
            await _initLock.WaitAsync();
            try
            {
                if (_database != null)
                {
                    await _database.CloseAsync();
                    _database = null;
                }
                await work();
            }
            finally
            {
                _initLock.Release();
            }
        }

        /// <summary>
        /// Writes a copy of <paramref name="source"/> to <paramref name="target"/> with the other key (null = plain)
        /// and checks it. The source is never changed; a failed copy is deleted.
        /// </summary>
        private static void Rekey(string source, string target, string? fromKey, string? toKey)
        {
            DeleteFile(target);
            try
            {
                File.Copy(source, target);
                using (var connection = new SQLiteConnection(ConnectionString(target, fromKey)))
                    connection.ExecuteScalar<string>($"PRAGMA rekey = '{toKey}'");

                // Plain SQLite ignores PRAGMA rekey: make sure the file really changed
                if (IsPlainSqliteFile(target) != (toKey == null))
                    throw new InvalidOperationException("The database file was not re-encrypted.");

                using var check = new SQLiteConnection(ConnectionString(target, toKey));
                var result = check.ExecuteScalar<string>("PRAGMA integrity_check");
                if (result != "ok")
                    throw new InvalidDataException($"The re-encrypted database failed its integrity check: {result}");
            }
            catch
            {
                DeleteFile(target);
                throw;
            }
        }

        private static bool CanOpen(string path, string key)
        {
            try
            {
                using var connection = new SQLiteConnection(ConnectionString(path, key));
                connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master");
                return true;
            }
            catch (SQLiteException ex) when (ex.Result == SQLite3.Result.NonDBFile)
            {
                // "file is not a database": not this key. Any other error (busy, I/O, full disk) is thrown, so the
                // file isn't set aside and the next start tries again.
                return false;
            }
        }

        private static SQLiteConnectionString ConnectionString(string path, string? key) =>
            new(path, OpenFlags, storeDateTimeAsTicks: true, key: key);

        private static bool IsPlainSqliteFile(string path)
        {
            ReadOnlySpan<byte> header = "SQLite format 3\0"u8;
            Span<byte> start = stackalloc byte[header.Length];
            using var file = File.OpenRead(path);
            return file.ReadAtLeast(start, start.Length, throwOnEndOfStream: false) == start.Length && start.SequenceEqual(header);
        }

        /// <summary>The moneyrecord*.db3.bak-v{n} copies taken before migrations.</summary>
        private static void DeleteMigrationCopies(string databasePath)
        {
            var folder = Path.GetDirectoryName(databasePath)!;
            foreach (var copy in Directory.GetFiles(folder, Path.GetFileName(databasePath) + MigrationRunner.BackupSuffix + "*"))
                DeleteFile(copy);
        }

        private static void DeleteFile(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        // ---- Schema ----

        /// <summary>
        /// Runs <paramref name="work"/> in one SQLite transaction: all of it is written, or nothing when it throws.
        /// For changes that span tables (delete cascades, restore) or a consistent read of several tables.
        /// </summary>
        public async Task RunInTransactionAsync(Action<SQLiteConnection> work)
        {
            var db = await GetConnectionAsync();
            await db.RunInTransactionAsync(work);
        }

        /// <summary>
        /// Creates or updates the tables from the models (latest schema). A new database is stamped with
        /// the latest version; a database from an older app version runs its pending migrations.
        /// </summary>
        public static async Task CreateSchemaAsync(SQLiteAsyncConnection db, MigrationRunner migrations)
        {
            var isNewDatabase = await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table'") == 0;

            await db.CreateTableAsync<Category>();
            await db.CreateTableAsync<Transaction>();
            await db.CreateTableAsync<Account>();
            await db.CreateTableAsync<Transfer>();
            await db.CreateTableAsync<Budget>();
            await db.CreateTableAsync<RecurringTransaction>();
            await db.CreateTableAsync<ExchangeRate>();
            await db.CreateTableAsync<SavingsGoal>();
            await db.CreateTableAsync<SavingsGoalAccount>();
            await db.CreateTableAsync<Payee>();
            await db.CreateTableAsync<Debt>();
            await db.CreateTableAsync<Trip>();
            await db.CreateTableAsync<TripCategory>();
            await db.CreateTableAsync<TripBudget>();

            if (isNewDatabase)
                await migrations.StampLatestAsync(db);
            else
                await migrations.RunAsync(db);
        }

        private static async Task SeedDefaultDataAsync(SQLiteAsyncConnection db)
        {
            if (await db.Table<Category>().CountAsync() == 0)
                await AddDefaultCategoriesAsync(db);

            if (await db.Table<Account>().CountAsync() == 0)
                await AddDefaultAccountAsync(db);

            if (await db.Table<TripCategory>().CountAsync() == 0)
                await db.RunInTransactionAsync(SeedTripCategories);
        }

        /// <summary>Inserts the default trip categories (a new database, an old one, and a restore of a backup that has none).</summary>
        internal static void SeedTripCategories(SQLiteConnection db)
        {
            foreach (var category in DefaultTripCategory.All)
                db.Insert(category.ToTripCategory());
        }

        private static async Task AddDefaultCategoriesAsync(SQLiteAsyncConnection db)
        {
            foreach (var category in DefaultCategory.All)
            {
                await db.InsertAsync(category.ToCategory());
            }
        }

        private static async Task AddDefaultAccountAsync(SQLiteAsyncConnection db)
        {
            var cashAccount = new Account
            {
                Name = AppResources.DefaultAccountCash,
                InitialBalance = 0,
                IsDefault = true,
                IconCode = Account.CashIconCode,
                // Cash starts at 0, so a new user's first expense must not be blocked
                AllowNegativeBalance = true,
                CurrencyCode = CurrencyService.GetDeviceCurrencyCode()
            };
            await db.InsertAsync(cashAccount);
        }
    }
}
