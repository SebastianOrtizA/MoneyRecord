using SQLite;

namespace MoneyRecord.Services.Repositories.Migrations
{
    /// <summary>
    /// Indexes for the date-range and per-account/category lookups every screen runs.
    /// </summary>
    public sealed class M002_Indexes : IMigration
    {
        public int Version => 2;
        public bool RewritesData => false;

        public void Up(SQLiteConnection db)
        {
            // "Transaction" is an SQL keyword, so table names are quoted
            db.Execute("CREATE INDEX IF NOT EXISTS IX_Transaction_Date ON \"Transaction\" (Date)");
            db.Execute("CREATE INDEX IF NOT EXISTS IX_Transaction_AccountId ON \"Transaction\" (AccountId)");
            db.Execute("CREATE INDEX IF NOT EXISTS IX_Transaction_CategoryId ON \"Transaction\" (CategoryId)");
            db.Execute("CREATE INDEX IF NOT EXISTS IX_Transfer_Date ON \"Transfer\" (Date)");
            db.Execute("CREATE INDEX IF NOT EXISTS IX_Transfer_SourceAccountId ON \"Transfer\" (SourceAccountId)");
            db.Execute("CREATE INDEX IF NOT EXISTS IX_Transfer_DestinationAccountId ON \"Transfer\" (DestinationAccountId)");
        }
    }
}
