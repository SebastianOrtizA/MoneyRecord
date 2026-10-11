using SQLite;

namespace MoneyRecord.Services.Repositories.Migrations
{
    /// <summary>
    /// The schema as it was before migrations existed (tables are created by CreateTableAsync).
    /// </summary>
    public sealed class M001_Baseline : IMigration
    {
        public int Version => 1;
        public bool RewritesData => false;

        public void Up(SQLiteConnection db)
        {
        }
    }
}
