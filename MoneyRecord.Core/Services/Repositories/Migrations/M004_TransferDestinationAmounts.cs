using SQLite;

namespace MoneyRecord.Services.Repositories.Migrations
{
    /// <summary>
    /// Transfers from before the destination amount moved the same number into both accounts (even between
    /// currencies), so what arrived is what left: copies AmountCents into the new DestinationAmountCents column.
    /// </summary>
    public sealed class M004_TransferDestinationAmounts : IMigration
    {
        public int Version => 4;
        public bool RewritesData => false;

        public void Up(SQLiteConnection db) => FillMissing(db);

        /// <summary>Gives every transfer without a destination amount its source amount (also used by restore).</summary>
        public static int FillMissing(SQLiteConnection db) =>
            db.Execute("UPDATE \"Transfer\" SET DestinationAmountCents = AmountCents " +
                       "WHERE DestinationAmountCents IS NULL OR DestinationAmountCents = 0");
    }
}
