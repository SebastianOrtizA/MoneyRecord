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

        // Deliberately reuses the all-accounts calculation: four indexed GROUP BY queries over a handful of accounts
        // cost about the same as per-account ones, and the balance rule then lives in one place.
        public async Task<decimal> GetAccountBalanceAsync(int accountId) =>
            (await GetAllAccountBalancesAsync()).FirstOrDefault(b => b.AccountId == accountId)?.CurrentBalance ?? 0;

        public Task<List<CurrencyAmount>> GetTotalIncomesAsync(DateTime startDate, DateTime endDate) =>
            _transactionRepository.SumByCurrencyAsync(TransactionType.Income, startDate, endDate, BaseCurrencyCode);

        public Task<List<CurrencyAmount>> GetTotalExpensesAsync(DateTime startDate, DateTime endDate) =>
            _transactionRepository.SumByCurrencyAsync(TransactionType.Expense, startDate, endDate, BaseCurrencyCode);

        public async Task<List<CurrencyAmount>> GetCategoryExpensesAsync(int categoryId, DateTime startDate, DateTime endDate) =>
            (await GetExpensesByCategoryAsync(startDate, endDate)).GetValueOrDefault(categoryId) ?? [];

        public Task<Dictionary<int, List<CurrencyAmount>>> GetExpensesByCategoryAsync(DateTime startDate, DateTime endDate) =>
            _transactionRepository.SumByCategoryAsync(TransactionType.Expense, startDate, endDate, BaseCurrencyCode);

        private static string BaseCurrencyCode => MoneyFormatter.ResolveCurrencyCode(null);

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
