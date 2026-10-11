using SQLite;

namespace MoneyRecord.Services.Repositories.Migrations
{
    /// <summary>
    /// Moves money from the REAL columns (sqlite-net's lossy decimal storage) into integer cents columns.
    /// CreateTableAsync has already added the *Cents columns; the old REAL columns stay, unused.
    /// </summary>
    public sealed class M002_MoneyAsCents : IMigration
    {
        public int Version => 2;
        public bool RewritesData => true;

        public void Up(SQLiteConnection db)
        {
            CopyToCents(db, "Transaction", "Amount", "AmountCents");
            CopyToCents(db, "Transfer", "Amount", "AmountCents");
            CopyToCents(db, "RecurringTransaction", "Amount", "AmountCents");
            CopyToCents(db, "Account", "InitialBalance", "InitialBalanceCents");
            CopyToCents(db, "Budget", "LimitAmount", "LimitAmountCents");
        }

        private static void CopyToCents(SQLiteConnection db, string table, string decimalColumn, string centsColumn)
        {
            // A table this app version just created (an older install never had it) has no legacy column
            if (!db.GetTableInfo(table).Any(c => c.Name == decimalColumn))
                return;

            // ROUND(x) rounds half away from zero, matching Money.ToCents
            db.Execute(
                $"UPDATE \"{table}\" SET \"{centsColumn}\" = CAST(ROUND(COALESCE(\"{decimalColumn}\", 0) * 100) AS INTEGER)");
        }
    }
}
