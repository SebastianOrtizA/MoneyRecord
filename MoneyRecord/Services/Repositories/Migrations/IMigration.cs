using SQLite;

namespace MoneyRecord.Services.Repositories.Migrations
{
    /// <summary>
    /// One schema step. Versions start at 1 and have no gaps; the database stores the last applied
    /// version in PRAGMA user_version.
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
