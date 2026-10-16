using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class LedgerService : ILedgerService
    {
        private const string TransferIconCode = "F0A27"; // bank-transfer

        private readonly ITransactionEnrichmentService _enrichmentService;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ITransferRepository _transferRepository;
        private readonly IAppPaths _paths;

        public LedgerService(ITransactionEnrichmentService enrichmentService, ITransactionRepository transactionRepository,
            ITransferRepository transferRepository, IAppPaths paths)
        {
            _enrichmentService = enrichmentService;
            _transactionRepository = transactionRepository;
            _transferRepository = transferRepository;
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

        public async Task DeleteAsync(LedgerEntry entry)
        {
            if (entry.IsTransfer)
            {
                var transfer = await _transferRepository.GetByIdAsync(entry.SourceId);
                if (transfer != null)
                    await _transferRepository.DeleteAsync(transfer);
                return;
            }

            var transaction = await _transactionRepository.GetByIdAsync(entry.SourceId);
            if (transaction != null)
            {
                await _transactionRepository.DeleteAsync(transaction);
                ReceiptFiles.Delete(transaction.ReceiptPhotoPath, _paths.ReceiptsFolder);
            }
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
