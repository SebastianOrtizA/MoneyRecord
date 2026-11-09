using MoneyRecord.Helpers;
using MoneyRecord.Models;
using SQLite;

namespace MoneyRecord.Services.Repositories.Migrations
{
    /// <summary>Fills the new TransactionTag table (one row per tag of a transaction) from the transactions' comma-separated tags.</summary>
    public sealed class M005_TransactionTags : IMigration
    {
        public int Version => 5;
        public bool RewritesData => true;

        public void Up(SQLiteConnection db)
        {
            db.CreateTable<TransactionTag>(); // a no-op where the schema step has made it
            TransactionTags.RebuildAll(db);
        }
    }
}
