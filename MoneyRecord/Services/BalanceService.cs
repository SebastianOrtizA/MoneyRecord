using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class BalanceService : IBalanceService
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ITransferRepository _transferRepository;

        public BalanceService(
            IAccountRepository accountRepository,
            ITransactionRepository transactionRepository,
            ITransferRepository transferRepository)
        {
            _accountRepository = accountRepository;
            _transactionRepository = transactionRepository;
            _transferRepository = transferRepository;
        }

        public async Task<decimal> GetTotalBalanceAsync()
        {
            var accounts = await _accountRepository.GetAllAsync();
            decimal totalInitialBalance = accounts.Sum(a => a.InitialBalance);

            var allTransactions = await _transactionRepository.GetAllAsync();

            var totalIncomes = allTransactions
                .Where(t => t.Type == TransactionType.Income)
                .Sum(t => Math.Abs(t.Amount));
            var totalExpenses = allTransactions
                .Where(t => t.Type == TransactionType.Expense)
                .Sum(t => Math.Abs(t.Amount));

            return totalInitialBalance + totalIncomes - totalExpenses;
        }

        public async Task<decimal> GetAccountBalanceAsync(int accountId)
        {
            var account = await _accountRepository.GetByIdAsync(accountId);
            if (account == null)
                return 0;

            var transactions = await _transactionRepository.GetByAccountIdAsync(accountId);

            var incomes = transactions
                .Where(t => t.Type == TransactionType.Income)
                .Sum(t => Math.Abs(t.Amount));
            var expenses = transactions
                .Where(t => t.Type == TransactionType.Expense)
                .Sum(t => Math.Abs(t.Amount));

            var outgoingTransfers = await _transferRepository.GetBySourceAccountIdAsync(accountId);
            var incomingTransfers = await _transferRepository.GetByDestinationAccountIdAsync(accountId);

            var transfersOut = outgoingTransfers.Sum(t => Math.Abs(t.Amount));
            var transfersIn = incomingTransfers.Sum(t => Math.Abs(t.Amount));

            return account.InitialBalance + incomes - expenses - transfersOut + transfersIn;
        }

        public async Task<decimal> GetTotalIncomesAsync(DateTime startDate, DateTime endDate)
        {
            var transactions = await _transactionRepository.GetByDateRangeAsync(startDate, endDate);
            return transactions
                .Where(t => t.Type == TransactionType.Income)
                .Sum(t => Math.Abs(t.Amount));
        }

        public async Task<decimal> GetTotalExpensesAsync(DateTime startDate, DateTime endDate)
        {
            var transactions = await _transactionRepository.GetByDateRangeAsync(startDate, endDate);
            return transactions
                .Where(t => t.Type == TransactionType.Expense)
                .Sum(t => Math.Abs(t.Amount));
        }

        public async Task<decimal> GetBalanceAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            List<Transaction> transactions;

            if (startDate.HasValue && endDate.HasValue)
            {
                transactions = await _transactionRepository.GetByDateRangeAsync(startDate.Value, endDate.Value);
            }
            else
            {
                transactions = await _transactionRepository.GetAllAsync();
            }

            var incomes = transactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
            var expenses = transactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);

            return incomes - expenses;
        }

        public async Task<List<AccountBalanceInfo>> GetAllAccountBalancesAsync()
        {
            var accountsTask = _accountRepository.GetAllAsync();
            var allTransactionsTask = _transactionRepository.GetAllAsync();
            var allTransfersTask = _transferRepository.GetAllAsync();

            await Task.WhenAll(accountsTask, allTransactionsTask, allTransfersTask);

            var accounts = await accountsTask;
            var allTransactions = await allTransactionsTask;
            var allTransfers = await allTransfersTask;

            var transactionsByAccount = allTransactions
                .Where(t => t.AccountId.HasValue)
                .GroupBy(t => t.AccountId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            var transfersBySource = allTransfers
                .GroupBy(t => t.SourceAccountId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var transfersByDest = allTransfers
                .GroupBy(t => t.DestinationAccountId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var result = new List<AccountBalanceInfo>(accounts.Count);

            foreach (var account in accounts)
            {
                var accountTransactions = transactionsByAccount.GetValueOrDefault(account.Id, []);
                var incomes = accountTransactions
                    .Where(t => t.Type == TransactionType.Income)
                    .Sum(t => Math.Abs(t.Amount));
                var expenses = accountTransactions
                    .Where(t => t.Type == TransactionType.Expense)
                    .Sum(t => Math.Abs(t.Amount));

                var outgoing = transfersBySource.GetValueOrDefault(account.Id, []);
                var incoming = transfersByDest.GetValueOrDefault(account.Id, []);
                var transfersOut = outgoing.Sum(t => Math.Abs(t.Amount));
                var transfersIn = incoming.Sum(t => Math.Abs(t.Amount));

                var balance = account.InitialBalance + incomes - expenses - transfersOut + transfersIn;

                var activityDates = new List<DateTime>();
                if (accountTransactions.Count > 0)
                    activityDates.Add(accountTransactions.Max(t => t.Date));
                var accountTransfers = outgoing.Concat(incoming).ToList();
                if (accountTransfers.Count > 0)
                    activityDates.Add(accountTransfers.Max(t => t.Date));

                result.Add(new AccountBalanceInfo
                {
                    AccountId = account.Id,
                    AccountName = account.Name ?? string.Empty,
                    CurrentBalance = balance,
                    LastActivityDate = activityDates.Count > 0 ? activityDates.Max() : account.CreatedDate
                });
            }

            return result;
        }

        public async Task<DateTime?> GetLastTransactionDateForAccountAsync(int accountId)
        {
            var transactions = await _transactionRepository.GetByAccountIdAsync(accountId);
            return transactions
                .OrderByDescending(t => t.Date)
                .FirstOrDefault()?.Date;
        }

        public async Task<decimal> GetCategoryExpensesAsync(int categoryId, DateTime startDate, DateTime endDate)
        {
            var transactions = await _transactionRepository.GetByDateRangeAsync(startDate, endDate);
            return transactions
                .Where(t => t.CategoryId == categoryId && t.Type == TransactionType.Expense)
                .Sum(t => Math.Abs(t.Amount));
        }
    }
}
