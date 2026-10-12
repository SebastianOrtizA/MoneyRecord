using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>Balances and totals, summed by SQLite (no transaction rows are loaded).</summary>
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

        public async Task<decimal> GetAccountBalanceAsync(int accountId) =>
            (await GetAllAccountBalancesAsync()).FirstOrDefault(b => b.AccountId == accountId)?.CurrentBalance ?? 0;

        public async Task<decimal> GetTotalIncomesAsync(DateTime startDate, DateTime endDate) =>
            Money.FromCents(await _transactionRepository.SumCentsAsync(TransactionType.Income, startDate, endDate));

        public async Task<decimal> GetTotalExpensesAsync(DateTime startDate, DateTime endDate) =>
            Money.FromCents(await _transactionRepository.SumCentsAsync(TransactionType.Expense, startDate, endDate));

        public async Task<decimal> GetCategoryExpensesAsync(int categoryId, DateTime startDate, DateTime endDate) =>
            Money.FromCents(await _transactionRepository.SumCentsAsync(TransactionType.Expense, startDate, endDate, categoryId));

        public async Task<Dictionary<int, decimal>> GetExpensesByCategoryAsync(DateTime startDate, DateTime endDate) =>
            (await _transactionRepository.SumCentsByCategoryAsync(TransactionType.Expense, startDate, endDate))
                .ToDictionary(kv => kv.Key, kv => Money.FromCents(kv.Value));

        public async Task<List<AccountBalanceInfo>> GetAllAccountBalancesAsync()
        {
            var accountsTask = _accountRepository.GetAllAsync();
            var incomesTask = _transactionRepository.SumCentsByAccountAsync(TransactionType.Income);
            var expensesTask = _transactionRepository.SumCentsByAccountAsync(TransactionType.Expense);
            var outgoingTask = _transferRepository.SumCentsBySourceAsync();
            var incomingTask = _transferRepository.SumCentsByDestinationAsync();
            await Task.WhenAll(accountsTask, incomesTask, expensesTask, outgoingTask, incomingTask);

            var incomes = (await incomesTask).ToDictionary(t => t.AccountId);
            var expenses = (await expensesTask).ToDictionary(t => t.AccountId);
            var outgoing = (await outgoingTask).ToDictionary(t => t.AccountId);
            var incoming = (await incomingTask).ToDictionary(t => t.AccountId);

            return (await accountsTask).Select(account =>
            {
                var income = incomes.GetValueOrDefault(account.Id);
                var expense = expenses.GetValueOrDefault(account.Id);
                var transferOut = outgoing.GetValueOrDefault(account.Id);
                var transferIn = incoming.GetValueOrDefault(account.Id);

                return new AccountBalanceInfo
                {
                    AccountId = account.Id,
                    AccountName = account.Name ?? string.Empty,
                    CurrencyCode = account.CurrencyCode,
                    CurrentBalance = account.InitialBalance
                        + Amount(income) - Amount(expense) - Amount(transferOut) + Amount(transferIn),
                    // The latest transaction or transfer; the creation date for an account without any
                    LastActivityDate = new[] { income, expense, transferOut, transferIn }
                        .OfType<AccountTotal>()
                        .Select(t => t.LastDate)
                        .DefaultIfEmpty(account.CreatedDate)
                        .Max()
                };
            }).ToList();
        }

        private static decimal Amount(AccountTotal? total) => total?.Amount ?? 0;
    }
}
