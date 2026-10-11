using SQLite;

namespace MoneyRecord.Services.Repositories.Migrations
{
    /// <summary>
    /// Moves money from the REAL columns (sqlite-net's lossy decimal storage) into integer cents columns.
    /// CreateTableAsync has already added the *Cents columns; the old REAL columns stay, unused.
    /// </summary>
    public sealed class M002_MoneyAsCents : IMigration
    {
        // Frozen copies of the rules at the time of this migration, so later changes to Money can't alter it
        private const decimal MaxAmount = 999_999_999_999.99m;
        private const int CentsPerUnit = 100;

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

        /// <summary>
        /// Converts in C#, not SQL ROUND: the app read each REAL through (decimal)double, which keeps 15 significant
        /// digits, so a stored 1.0049999999999999 was shown and summed as 1.005 ($1.01). ROUND(x * 100) in SQL works
        /// in binary floating point and would turn it into 100 cents.
        /// </summary>
        public static long ToCents(double? stored)
        {
            var value = stored ?? 0;
            if (double.IsNaN(value))
                return 0;

            // Clamp absurd legacy values instead of failing the migration (and with it, every app start)
            var amount = Math.Abs(value) > (double)MaxAmount
                ? Math.Sign(value) * MaxAmount
                : (decimal)value;

            return (long)Math.Round(amount * CentsPerUnit, MidpointRounding.AwayFromZero);
        }

        private static void CopyToCents(SQLiteConnection db, string table, string decimalColumn, string centsColumn)
        {
            // A table this app version just created (an older install never had it) has no legacy column
            if (!db.GetTableInfo(table).Any(c => c.Name == decimalColumn))
                return;

            var rows = db.Query<LegacyRow>($"SELECT Id, \"{decimalColumn}\" AS Value FROM \"{table}\"");
            foreach (var row in rows)
                db.Execute($"UPDATE \"{table}\" SET \"{centsColumn}\" = ? WHERE Id = ?", ToCents(row.Value), row.Id);
        }

        private sealed class LegacyRow
        {
            public int Id { get; set; }
            public double? Value { get; set; }
        }
    }
}
