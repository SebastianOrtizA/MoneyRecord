using MoneyRecord.Models;
using SQLite;

namespace MoneyRecord.Helpers
{
    /// <summary>Reads and writes the <see cref="TransactionAttachment"/> rows and keeps <see cref="Transaction.ReceiptPhotoPath"/> (the first photo) in step.</summary>
    public static class TransactionAttachments
    {
        /// <summary>The path of the first photo, or null: what <see cref="Transaction.ReceiptPhotoPath"/> holds.</summary>
        public static string? PrimaryPhoto(IEnumerable<TransactionAttachment> attachments) =>
            attachments.FirstOrDefault(a => a.Kind == AttachmentKind.Photo)?.Path;

        public static AttachmentKind KindOf(string path) =>
            string.Equals(System.IO.Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase)
                ? AttachmentKind.Pdf
                : AttachmentKind.Photo;

        /// <summary>
        /// Saves <see cref="Transaction.PendingAttachments"/> (when set) as the transaction's rows and sets its
        /// <see cref="Transaction.ReceiptPhotoPath"/>. Call after the transaction is saved (it needs its ID).
        /// </summary>
        public static void Sync(SQLiteConnection db, IEnumerable<Transaction> transactions)
        {
            foreach (var transaction in transactions)
            {
                if (transaction.PendingAttachments is not { } attachments)
                    continue;

                db.Execute("DELETE FROM \"TransactionAttachment\" WHERE TransactionId = ?", transaction.Id);
                foreach (var attachment in attachments)
                {
                    db.Insert(new TransactionAttachment
                    {
                        TransactionId = transaction.Id,
                        Path = attachment.Path,
                        Kind = attachment.Kind,
                        Name = attachment.Name,
                        AddedDate = attachment.AddedDate
                    });
                }

                var primary = PrimaryPhoto(attachments);
                if (transaction.ReceiptPhotoPath != primary)
                {
                    transaction.ReceiptPhotoPath = primary;
                    db.Execute("UPDATE \"Transaction\" SET ReceiptPhotoPath = ? WHERE Id = ?", primary, transaction.Id);
                }
            }
        }

        /// <summary>The attachments of these transactions, by transaction ID, in the order they were added.</summary>
        public static Dictionary<int, List<TransactionAttachment>> Load(SQLiteConnection db, IReadOnlyCollection<int> transactionIds)
        {
            var wanted = transactionIds.ToHashSet();
            return db.Table<TransactionAttachment>().OrderBy(a => a.Id).ToList()
                .Where(a => wanted.Contains(a.TransactionId))
                .GroupBy(a => a.TransactionId)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        /// <summary>Points <see cref="Transaction.ReceiptPhotoPath"/> of every transaction that has attachments at its first photo (a restore may have dropped a missing file).</summary>
        public static void RefreshPrimaryPhotos(SQLiteConnection db)
        {
            var attachments = db.Table<TransactionAttachment>().OrderBy(a => a.Id).ToList().GroupBy(a => a.TransactionId);
            foreach (var group in attachments)
                db.Execute("UPDATE \"Transaction\" SET ReceiptPhotoPath = ? WHERE Id = ?", PrimaryPhoto(group), group.Key);
        }

        /// <summary>One photo row for each transaction that has a <see cref="Transaction.ReceiptPhotoPath"/> but no rows yet (a migration, an old backup).</summary>
        public static void FillFromReceipts(SQLiteConnection db)
        {
            foreach (var transaction in db.Query<Transaction>(
                         "SELECT * FROM \"Transaction\" WHERE ReceiptPhotoPath IS NOT NULL AND ReceiptPhotoPath <> '' " +
                         "AND Id NOT IN (SELECT TransactionId FROM \"TransactionAttachment\")"))
            {
                db.Insert(new TransactionAttachment
                {
                    TransactionId = transaction.Id,
                    Path = transaction.ReceiptPhotoPath!,
                    Kind = AttachmentKind.Photo,
                    Name = System.IO.Path.GetFileName(transaction.ReceiptPhotoPath!),
                    AddedDate = transaction.Date
                });
            }
        }
    }
}
