using MoneyRecord.Helpers;
using MoneyRecord.Models;
using SQLite;

namespace MoneyRecord.Services.Repositories.Migrations
{
    /// <summary>Gives each existing receipt photo a TransactionAttachment row. The photo files and ReceiptPhotoPath are left as they are.</summary>
    public sealed class M006_TransactionAttachments : IMigration
    {
        public int Version => 6;
        public bool RewritesData => true;

        public void Up(SQLiteConnection db)
        {
            db.CreateTable<TransactionAttachment>(); // a no-op where the schema step has made it
            TransactionAttachments.FillFromReceipts(db);
        }
    }
}
