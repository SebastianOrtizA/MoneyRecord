using MoneyRecord.Helpers;
using SQLite;

namespace MoneyRecord.Services.Repositories.Migrations
{
    /// <summary>
    /// Accounts from before the currency column have an empty CurrencyCode, which would otherwise follow the base
    /// currency whenever the user changes it (relabelling their money and changing which accounts count as the same
    /// currency). Stamps them with the currency their amounts were shown in: the base currency at upgrade time.
    /// </summary>
    public sealed class M003_AccountCurrencyCodes : IMigration
    {
        public int Version => 3;
        public bool RewritesData => false;

        public void Up(SQLiteConnection db) => FillMissing(db, MoneyFormatter.ResolveCurrencyCode(null));

        /// <summary>Gives every account without a currency <paramref name="currencyCode"/> (also used by restore).</summary>
        public static int FillMissing(SQLiteConnection db, string currencyCode) =>
            db.Execute("UPDATE \"Account\" SET CurrencyCode = ? WHERE CurrencyCode IS NULL OR CurrencyCode = ''", currencyCode);
    }
}
