using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>
    /// Service for calculating financial balances.
    /// Single responsibility: Balance calculations.
    /// </summary>
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
            var accounts = await _accountRepository.GetAllAsync();
            var result = new List<AccountBalanceInfo>();

            foreach (var account in accounts)
            {
                var balance = await GetAccountBalanceAsync(account.Id);
                var lastActivityDate = await GetLastActivityDateForAccountAsync(account.Id, account.CreatedDate);

                result.Add(new AccountBalanceInfo
                {
                    AccountId = account.Id,
                    AccountName = account.Name ?? string.Empty,
                    CurrentBalance = balance,
                    LastActivityDate = lastActivityDate
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

        private async Task<DateTime?> GetLastActivityDateForAccountAsync(int accountId, DateTime createdDate)
        {
            var activityDates = new List<DateTime>();

            var transactions = await _transactionRepository.GetByAccountIdAsync(accountId);
            if (transactions.Count > 0)
            {
                activityDates.Add(transactions.Max(t => t.Date));
            }

            var transfers = await _transferRepository.GetByAccountIdAsync(accountId);
            if (transfers.Count > 0)
            {
                activityDates.Add(transfers.Max(t => t.Date));
            }

            return activityDates.Count > 0 ? activityDates.Max() : createdDate;
        }
    }
}
