using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Helpers
{
    public static class LedgerTransfers
    {
        /// <summary>
        /// The entries for the account view: each whole transfer becomes two halves, out of the source and into the
        /// destination, so both accounts list it. Other entries are kept as they are.
        /// </summary>
        public static List<LedgerEntry> PerAccount(IEnumerable<LedgerEntry> entries) => entries
            .SelectMany(e => e.Kind == LedgerEntryKind.Transfer
                ? new[] { Half(e, outgoing: true), Half(e, outgoing: false) }
                : new[] { e })
            .ToList();

        /// <summary>"Transfer to Savings: rent" on the source account, "Transfer from Checking: rent" on the destination.</summary>
        private static LedgerEntry Half(LedgerEntry transfer, bool outgoing) => transfer with
        {
            Kind = outgoing ? LedgerEntryKind.TransferOut : LedgerEntryKind.TransferIn,
            Description = outgoing
                ? Label(AppResources.TransferToAccount, transfer.CounterpartAccountName, transfer.TransferNote)
                : Label(AppResources.TransferFromAccount, transfer.AccountName, transfer.TransferNote),
            AccountId = outgoing ? transfer.AccountId : transfer.CounterpartAccountId,
            AccountName = outgoing ? transfer.AccountName : transfer.CounterpartAccountName,
            CounterpartAccountId = outgoing ? transfer.CounterpartAccountId : transfer.AccountId,
            CounterpartAccountName = outgoing ? transfer.CounterpartAccountName : transfer.AccountName
        };

        private static string Label(string format, string accountName, string note)
        {
            var label = string.Format(format, accountName);
            return string.IsNullOrEmpty(note) ? label : $"{label}: {note}";
        }
    }
}
