using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>
    /// Service for enriching transactions with related entity data.
    /// Optimizes data loading by batching queries and using in-memory lookups.
    /// </summary>
    public sealed class TransactionEnrichmentService : ITransactionEnrichmentService
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly ITransferRepository _transferRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IAccountRepository _accountRepository;

        public TransactionEnrichmentService(
            ITransactionRepository transactionRepository,
            ITransferRepository transferRepository,
            ICategoryRepository categoryRepository,
            IAccountRepository accountRepository)
        {
            _transactionRepository = transactionRepository;
            _transferRepository = transferRepository;
            _categoryRepository = categoryRepository;
            _accountRepository = accountRepository;
        }

        public async Task<List<Transaction>> GetEnrichedTransactionsAsync(DateTime startDate, DateTime endDate)
        {
            var transactionsTask = _transactionRepository.GetByDateRangeAsync(startDate, endDate);
            var categoriesTask = _categoryRepository.GetAllAsync();
            var accountsTask = _accountRepository.GetAllAsync();

            await Task.WhenAll(transactionsTask, categoriesTask, accountsTask);

            var transactions = await transactionsTask ?? [];
            var categories = (await categoriesTask ?? []).ToDictionary(c => c.Id);
            var accounts = (await accountsTask ?? []).ToDictionary(a => a.Id);

            foreach (var transaction in transactions)
            {
                EnrichTransaction(transaction, categories, accounts);
            }

            return transactions;
        }

        public async Task<List<Transfer>> GetEnrichedTransfersAsync(DateTime startDate, DateTime endDate)
        {
            var transfers = await _transferRepository.GetByDateRangeAsync(startDate, endDate);
            var accounts = (await _accountRepository.GetAllAsync()).ToDictionary(a => a.Id);

            foreach (var transfer in transfers)
            {
                EnrichTransfer(transfer, accounts);
            }

            return transfers;
        }

        public async Task<List<Transfer>> GetAllEnrichedTransfersAsync()
        {
            var transfers = await _transferRepository.GetAllAsync();
            var accounts = (await _accountRepository.GetAllAsync()).ToDictionary(a => a.Id);

            foreach (var transfer in transfers)
            {
                EnrichTransfer(transfer, accounts);
            }

            return transfers;
        }

        public async Task<Transfer?> GetEnrichedTransferAsync(int id)
        {
            var transfer = await _transferRepository.GetByIdAsync(id);
            if (transfer == null)
                return null;

            var accounts = (await _accountRepository.GetAllAsync()).ToDictionary(a => a.Id);
            EnrichTransfer(transfer, accounts);

            return transfer;
        }

        private static void EnrichTransaction(
            Transaction transaction,
            Dictionary<int, Category> categories,
            Dictionary<int, Account> accounts)
        {
            if (categories.TryGetValue(transaction.CategoryId, out var category))
            {
                transaction.CategoryName = category.Name ?? "Unknown";
                transaction.CategoryIconCode = category.IconCode ?? "F0770";
            }
            else
            {
                transaction.CategoryName = "Unknown";
                transaction.CategoryIconCode = "F0770";
            }

            if (transaction.AccountId.HasValue && accounts.TryGetValue(transaction.AccountId.Value, out var account))
            {
                transaction.AccountName = account.Name ?? "Cash";
                transaction.AccountIconCode = account.IconCode ?? "F0070";
            }
            else
            {
                transaction.AccountName = "Cash";
                transaction.AccountIconCode = "F0115";
            }
        }

        private static void EnrichTransfer(Transfer transfer, Dictionary<int, Account> accounts)
        {
            if (accounts.TryGetValue(transfer.SourceAccountId, out var sourceAccount))
            {
                transfer.SourceAccountName = sourceAccount.Name ?? "Unknown";
            }
            else
            {
                transfer.SourceAccountName = "Unknown";
            }

            if (accounts.TryGetValue(transfer.DestinationAccountId, out var destAccount))
            {
                transfer.DestinationAccountName = destAccount.Name ?? "Unknown";
            }
            else
            {
                transfer.DestinationAccountName = "Unknown";
            }
        }
    }
}
