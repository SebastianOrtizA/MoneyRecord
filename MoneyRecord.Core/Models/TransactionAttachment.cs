using SQLite;

namespace MoneyRecord.Models
{
    public enum AttachmentKind
    {
        Photo,
        Pdf
    }

    /// <summary>
    /// A file attached to a transaction (a receipt photo, an invoice PDF), stored in the receipts folder. Kept in step by
    /// <see cref="Helpers.TransactionAttachments"/>; a row left behind by a delete is harmless (always read by transaction ID).
    /// </summary>
    public class TransactionAttachment
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed(Name = "IX_TransactionAttachment_TransactionId")]
        public int TransactionId { get; set; }

        /// <summary>Full path inside the receipts folder.</summary>
        public string Path { get; set; } = string.Empty;

        public AttachmentKind Kind { get; set; }

        /// <summary>The name shown for a PDF (the picked file's name).</summary>
        public string Name { get; set; } = string.Empty;

        public DateTime AddedDate { get; set; }

        [Ignore, System.Text.Json.Serialization.JsonIgnore]
        public bool IsPhoto => Kind == AttachmentKind.Photo;

        [Ignore, System.Text.Json.Serialization.JsonIgnore]
        public bool IsPdf => Kind == AttachmentKind.Pdf;

        [Ignore, System.Text.Json.Serialization.JsonIgnore]
        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? System.IO.Path.GetFileName(Path) : Name;
    }
}
