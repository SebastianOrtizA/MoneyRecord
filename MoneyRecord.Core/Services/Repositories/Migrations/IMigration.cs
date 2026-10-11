using SQLite;

namespace MoneyRecord.Services.Repositories.Migrations
{
    /// <summary>
    /// One schema step. Versions start at 1 and have no gaps; the database stores the last applied
    /// version in PRAGMA user_version.
    /// The models always describe the latest schema: a new database is created from them and stamped
    /// with the latest version, so migrations never run on it. A migration only transforms a database
    /// created by an older app version, after CreateTableAsync has added any new model columns and indexes
    /// (sqlite-net never drops or renames columns, so old columns are still there to read from).
    /// </summary>
    public interface IMigration
    {
        int Version { get; }

        /// <summary>
        /// True when Up rewrites existing rows; the runner backs up the database file first.
        /// </summary>
        bool RewritesData { get; }

        /// <summary>
        /// Runs inside a transaction opened by the runner. Throwing rolls the whole step back.
        /// </summary>
        void Up(SQLiteConnection db);
    }
}
