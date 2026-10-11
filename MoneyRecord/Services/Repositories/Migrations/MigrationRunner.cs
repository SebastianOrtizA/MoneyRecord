using SQLite;

namespace MoneyRecord.Services.Repositories.Migrations
{
    /// <summary>
    /// Brings a database created by an older app version up to date. Each pending migration runs in
    /// its own transaction together with the PRAGMA user_version update, so a failed step leaves the
    /// database untouched. New databases are built from the models and only stamped with the latest version.
    /// </summary>
    public sealed class MigrationRunner
    {
        /// <summary>
        /// Every migration, in order. Add new ones at the end.
        /// </summary>
        public static readonly IReadOnlyList<IMigration> All =
        [
            new M001_Baseline()
        ];

        private readonly IReadOnlyList<IMigration> _migrations;

        public MigrationRunner(IEnumerable<IMigration> migrations)
        {
            _migrations = migrations.OrderBy(m => m.Version).ToList();

            for (var i = 0; i < _migrations.Count; i++)
            {
                if (_migrations[i].Version != i + 1)
                    throw new ArgumentException(
                        $"Migration versions must be 1..{_migrations.Count} with no gaps or duplicates.", nameof(migrations));
            }
        }

        public int LatestVersion => _migrations.Count;

        /// <summary>
        /// Applies every pending migration and returns the resulting schema version.
        /// </summary>
        /// <exception cref="InvalidOperationException">The database was written by a newer app version.</exception>
        public async Task<int> RunAsync(SQLiteAsyncConnection db)
        {
            var version = await GetVersionAsync(db);
            if (version > LatestVersion)
                throw new InvalidOperationException(
                    $"The database is at schema version {version}, but this app only knows up to {LatestVersion}. " +
                    "It was probably restored from a newer app version.");

            foreach (var migration in _migrations.Where(m => m.Version > version))
            {
                if (migration.RewritesData)
                    await BackupAsync(db, migration.Version);

                await db.RunInTransactionAsync(connection =>
                {
                    migration.Up(connection);
                    connection.Execute($"PRAGMA user_version = {migration.Version}");
                });
                version = migration.Version;
            }

            return version;
        }

        /// <summary>
        /// Marks a database freshly built from the current models as fully migrated.
        /// </summary>
        public Task StampLatestAsync(SQLiteAsyncConnection db) =>
            db.ExecuteAsync($"PRAGMA user_version = {LatestVersion}");

        public static Task<int> GetVersionAsync(SQLiteAsyncConnection db) =>
            db.ExecuteScalarAsync<int>("PRAGMA user_version");

        public static string GetBackupPath(string databasePath, int version) => $"{databasePath}.bak-v{version}";

        /// <summary>
        /// Copies the database to moneyrecord.db3.bak-v{version}. VACUUM INTO is safe on an open connection.
        /// </summary>
        private static async Task BackupAsync(SQLiteAsyncConnection db, int version)
        {
            var backupPath = GetBackupPath(db.DatabasePath, version);
            if (File.Exists(backupPath))
                File.Delete(backupPath);

            await db.ExecuteAsync("VACUUM INTO ?", backupPath);
        }
    }
}
