using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;
using SQLite;

namespace MoneyRecord.Services
{
    public sealed class LedgerService : ILedgerService
    {
        private const string TransferIconCode = "F0A27"; // bank-transfer

        private readonly ITransactionEnrichmentService _enrichmentService;
        private readonly DatabaseInitializer _database;
        private readonly IAppPaths _paths;

        public LedgerService(ITransactionEnrichmentService enrichmentService, DatabaseInitializer database, IAppPaths paths)
        {
            _enrichmentService = enrichmentService;
            _database = database;
            _paths = paths;
        }

        public async Task<List<LedgerEntry>> GetEntriesAsync(DateTime startDate, DateTime endDate)
        {
            var transactionsTask = _enrichmentService.GetEnrichedTransactionsAsync(startDate, endDate);
            var transfersTask = _enrichmentService.GetEnrichedTransfersAsync(startDate, endDate);
            await Task.WhenAll(transactionsTask, transfersTask);

            return (await transactionsTask).Select(FromTransaction)
                .Concat((await transfersTask).Select(FromTransfer))
                .ToList();
        }

        public async Task<DeletedEntry?> DeleteAsync(LedgerEntry entry)
        {
            if (entry.IsTransfer)
                return await DeleteTransferAsync(entry.SourceId);

            DeletedEntry? deleted = null;
            await _database.RunInTransactionAsync(db =>
            {
                // A split payment goes as a whole: its parts share the receipt
                var rows = entry.SplitId is { Length: > 0 } splitId
                    ? db.Table<Transaction>().Where(t => t.SplitId == splitId).OrderBy(t => t.Id).ToList()
                    : db.Find<Transaction>(entry.SourceId) is { } one ? new List<Transaction> { one } : [];
                if (rows.Count == 0)
                    return;

                foreach (var row in rows)
                    db.Delete<Transaction>(row.Id);
                deleted = new DeletedEntry(rows, null);
            });
            return deleted;
        }

        public async Task<DeletedEntry?> DeleteTransferAsync(int transferId)
        {
            DeletedEntry? deleted = null;
            await _database.RunInTransactionAsync(db =>
            {
                if (db.Find<Transfer>(transferId) is not { } transfer)
                    return;

                db.Delete<Transfer>(transferId);
                deleted = new DeletedEntry([], transfer);
            });
            return deleted;
        }

        public async Task<bool> RestoreAsync(DeletedEntry deleted)
        {
            var restored = false;
            await _database.RunInTransactionAsync(db =>
            {
                if (!CanRestore(db, deleted))
                    return;

                // sqlite-net only writes an AutoIncrement key with "OR REPLACE"; CanRestore saw that nothing has these IDs
                foreach (var transaction in deleted.Transactions)
                    db.Insert(transaction, "OR REPLACE");
                if (deleted.Transfer != null)
                    db.Insert(deleted.Transfer, "OR REPLACE");
                restored = true;
            });
            return restored;
        }

        public void Discard(DeletedEntry deleted)
        {
            foreach (var receipt in deleted.ReceiptPaths)
                ReceiptFiles.Delete(receipt, _paths.ReceiptsFolder);
        }

        private static bool CanRestore(SQLiteConnection db, DeletedEntry deleted)
        {
            foreach (var t in deleted.Transactions)
            {
                if (db.Find<Transaction>(t.Id) != null
                    || db.Find<Category>(t.CategoryId) == null
                    || (t.AccountId is { } account && db.Find<Account>(account) == null)
                    || (t.PayeeId is { } payee && db.Find<Payee>(payee) == null)
                    || (t.TripId is { } trip && db.Find<Trip>(trip) == null)
                    || (t.TripCategoryId is { } tripCategory && db.Find<TripCategory>(tripCategory) == null)
                    || (t.DebtId is { } debt && db.Find<Debt>(debt) == null))
                    return false;
            }

            return deleted.Transfer is not { } transfer
                || (db.Find<Transfer>(transfer.Id) == null
                    && db.Find<Account>(transfer.SourceAccountId) != null
                    && db.Find<Account>(transfer.DestinationAccountId) != null);
        }

        private static LedgerEntry FromTransaction(Transaction t) => new()
        {
            Kind = t.Type == TransactionType.Income ? LedgerEntryKind.Income : LedgerEntryKind.Expense,
            SourceId = t.Id,
            Date = t.Date,
            Description = t.Description,
            AmountCents = t.AmountCents,
            CurrencyCode = t.CurrencyCode,
            CategoryId = t.CategoryId,
            CategoryName = t.CategoryName,
            CategoryIconCode = t.CategoryIconCode,
            AccountId = t.AccountId,
            AccountName = t.AccountName,
            AccountIconCode = t.AccountIconCode,
            PayeeId = t.PayeeId,
            PayeeName = t.PayeeName,
            SplitId = t.SplitId,
            TripId = t.TripId,
            TripCategoryId = t.TripCategoryId,
            Tags = t.Tags,
            ReceiptPhotoPath = t.ReceiptPhotoPath
        };

        private static LedgerEntry FromTransfer(Transfer t) => new()
        {
            Kind = LedgerEntryKind.Transfer,
            SourceId = t.Id,
            Date = t.Date,
            Description = string.IsNullOrEmpty(t.Description)
                ? $"{t.SourceAccountName} → {t.DestinationAccountName}"
                : t.Description,
            TransferNote = t.Description ?? string.Empty,
            AmountCents = t.AmountCents,
            CurrencyCode = t.CurrencyCode,
            CategoryName = AppResources.Transfers,
            CategoryIconCode = TransferIconCode,
            AccountId = t.SourceAccountId,
            AccountName = t.SourceAccountName,
            AccountIconCode = TransferIconCode,
            CounterpartAccountId = t.DestinationAccountId,
            CounterpartAccountName = t.DestinationAccountName,
            CounterpartAmountCents = t.DestinationAmountCents,
            CounterpartCurrencyCode = t.DestinationCurrencyCode
        };
    }
}
