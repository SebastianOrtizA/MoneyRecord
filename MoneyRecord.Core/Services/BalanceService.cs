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
            _transactionRepository.SumByCurrencyAsync(TransactionType.Income, startDate, endDate, MoneyFormatter.BaseCurrency);

        public Task<List<CurrencyAmount>> GetTotalExpensesAsync(DateTime startDate, DateTime endDate) =>
            _transactionRepository.SumByCurrencyAsync(TransactionType.Expense, startDate, endDate, MoneyFormatter.BaseCurrency);

        public Task<Dictionary<int, List<CurrencyAmount>>> GetExpensesByCategoryAsync(DateTime startDate, DateTime endDate) =>
            _transactionRepository.SumByCategoryAsync(TransactionType.Expense, startDate, endDate, MoneyFormatter.BaseCurrency);

        public async Task<Dictionary<int, decimal>> GetChangesSinceAsync(DateTime since)
        {
            var incomesTask = _transactionRepository.SumCentsByAccountAsync(TransactionType.Income, since);
            var expensesTask = _transactionRepository.SumCentsByAccountAsync(TransactionType.Expense, since);
            var outgoingTask = _transferRepository.SumCentsBySourceAsync(since);
            var incomingTask = _transferRepository.SumCentsByDestinationAsync(since);
            await Task.WhenAll(incomesTask, expensesTask, outgoingTask, incomingTask);

            var changes = new Dictionary<int, decimal>();
            void Add(IEnumerable<AccountTotal> totals, int sign)
            {
                foreach (var total in totals)
                    changes[total.AccountId] = changes.GetValueOrDefault(total.AccountId) + sign * total.Amount;
            }

            Add(await incomesTask, 1);
            Add(await expensesTask, -1);
            Add(await outgoingTask, -1);
            Add(await incomingTask, 1);
            return changes;
        }

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
                    IsDebt = account.IsDebt,
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
