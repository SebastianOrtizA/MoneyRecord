using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class LedgerQueryService : ILedgerQueryService
    {
        private const string TransferIconCode = "F0A27"; // bank-transfer

        private readonly ITransactionEnrichmentService _enrichmentService;

        public LedgerQueryService(ITransactionEnrichmentService enrichmentService)
        {
            _enrichmentService = enrichmentService;
        }

        public async Task<List<LedgerEntry>> GetEntriesAsync(DateTime startDate, DateTime endDate, bool transfersPerAccount)
        {
            var transactionsTask = _enrichmentService.GetEnrichedTransactionsAsync(startDate, endDate);
            var transfersTask = _enrichmentService.GetEnrichedTransfersAsync(startDate, endDate);
            await Task.WhenAll(transactionsTask, transfersTask);

            var transferEntries = (await transfersTask).SelectMany(t => transfersPerAccount
                ? new[] { TransferHalf(t, outgoing: true), TransferHalf(t, outgoing: false) }
                : new[] { WholeTransfer(t) });

            return (await transactionsTask).Select(FromTransaction).Concat(transferEntries).ToList();
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
            Tags = t.Tags,
            ReceiptPhotoPath = t.ReceiptPhotoPath
        };

        private static LedgerEntry WholeTransfer(Transfer t) => TransferEntry(t) with
        {
            Kind = LedgerEntryKind.Transfer,
            Description = string.IsNullOrEmpty(t.Description)
                ? $"{t.SourceAccountName} → {t.DestinationAccountName}"
                : t.Description,
            AccountId = t.SourceAccountId,
            AccountName = t.SourceAccountName,
            CounterpartAccountId = t.DestinationAccountId,
            CounterpartAccountName = t.DestinationAccountName
        };

        /// <summary>"Transfer to Savings: rent" on the source account, "Transfer from Checking: rent" on the destination.</summary>
        private static LedgerEntry TransferHalf(Transfer t, bool outgoing) => TransferEntry(t) with
        {
            Kind = outgoing ? LedgerEntryKind.TransferOut : LedgerEntryKind.TransferIn,
            Description = outgoing
                ? TransferLabel(AppResources.TransferToAccount, t.DestinationAccountName, t.Description)
                : TransferLabel(AppResources.TransferFromAccount, t.SourceAccountName, t.Description),
            AccountId = outgoing ? t.SourceAccountId : t.DestinationAccountId,
            AccountName = outgoing ? t.SourceAccountName : t.DestinationAccountName,
            CounterpartAccountId = outgoing ? t.DestinationAccountId : t.SourceAccountId,
            CounterpartAccountName = outgoing ? t.DestinationAccountName : t.SourceAccountName
        };

        private static LedgerEntry TransferEntry(Transfer t) => new()
        {
            Kind = LedgerEntryKind.Transfer,
            SourceId = t.Id,
            Date = t.Date,
            AmountCents = t.AmountCents,
            CurrencyCode = t.CurrencyCode,
            CategoryName = AppResources.Transfers,
            CategoryIconCode = TransferIconCode,
            AccountIconCode = TransferIconCode
        };

        private static string TransferLabel(string format, string accountName, string? description)
        {
            var label = string.Format(format, accountName);
            return string.IsNullOrEmpty(description) ? label : $"{label}: {description}";
        }
    }
}
