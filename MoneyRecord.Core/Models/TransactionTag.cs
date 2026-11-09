using SQLite;

namespace MoneyRecord.Models
{
    /// <summary>
    /// One tag of one transaction: an index of <see cref="Transaction.Tags"/> (which stays the source and the backup's copy), kept
    /// in step by <see cref="Helpers.TransactionTags"/>. Always read joined to its transaction, so a row left behind by a delete is harmless.
    /// </summary>
    public class TransactionTag
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed(Name = "IX_TransactionTag_TransactionId")]
        public int TransactionId { get; set; }

        [Indexed(Name = "IX_TransactionTag_Tag")]
        public string Tag { get; set; } = string.Empty;
    }

    /// <summary>A tag with how many transactions use it and the latest of them: the Tags page.</summary>
    public sealed record TagSummary(string Name, int TransactionCount, DateTime? LastUsed);
}
