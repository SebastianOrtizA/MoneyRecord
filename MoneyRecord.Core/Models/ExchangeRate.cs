using SQLite;

namespace MoneyRecord.Models
{
    /// <summary>
    /// One downloaded daily rate: 1 <see cref="BaseCode"/> = <see cref="Rate"/> <see cref="QuoteCode"/> on <see cref="Date"/>.
    /// A cache (re-downloadable), so it is not in backups.
    /// </summary>
    public class ExchangeRate : IEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>The day (00:00) the rate is for.</summary>
        [Indexed(Name = "IX_ExchangeRate_Pair", Order = 3, Unique = true)]
        public DateTime Date { get; set; }

        [Indexed(Name = "IX_ExchangeRate_Pair", Order = 1, Unique = true)]
        public string BaseCode { get; set; } = string.Empty;

        [Indexed(Name = "IX_ExchangeRate_Pair", Order = 2, Unique = true)]
        public string QuoteCode { get; set; } = string.Empty;

        /// <summary>
        /// A double is enough: a rate isn't money, and its source publishes about 6 significant digits. Amounts are
        /// converted in decimal and rounded to the cent.
        /// </summary>
        public double Rate { get; set; }
    }
}
