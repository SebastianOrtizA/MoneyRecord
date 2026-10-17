using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>
    /// Balances and totals, summed by SQLite (no transaction rows are loaded). A balance counts the entries dated up to
    /// today: a future-dated one counts from its date on.
    /// </summary>
    public sealed class BalanceService : IBalanceService
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ITransferRepository _transferRepository;
        private readonly TimeProvider _clock;

        public BalanceService(
            IAccountRepository accountRepository,
            ITransactionRepository transactionRepository,
            ITransferRepository transferRepository,
            TimeProvider? clock = null)
        {
            _accountRepository = accountRepository;
            _transactionRepository = transactionRepository;
            _transferRepository = transferRepository;
            _clock = clock ?? TimeProvider.System;
        }

        /// <summary>The start of tomorrow: balances count what is dated before it.</summary>
        public DateTime CountedBefore => _clock.GetLocalNow().Date.AddDays(1);

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
            var before = CountedBefore;
            var incomesTask = _transactionRepository.SumCentsByAccountAsync(TransactionType.Income, since, before);
            var expensesTask = _transactionRepository.SumCentsByAccountAsync(TransactionType.Expense, since, before);
            var outgoingTask = _transferRepository.SumCentsBySourceAsync(since, before);
            var incomingTask = _transferRepository.SumCentsByDestinationAsync(since, before);
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
            var before = CountedBefore;
            var accountsTask = _accountRepository.GetAllAsync();
            var incomesTask = _transactionRepository.SumCentsByAccountAsync(TransactionType.Income, before: before);
            var expensesTask = _transactionRepository.SumCentsByAccountAsync(TransactionType.Expense, before: before);
            var outgoingTask = _transferRepository.SumCentsBySourceAsync(before: before);
            var incomingTask = _transferRepository.SumCentsByDestinationAsync(before: before);
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
                    // The latest counted transaction or transfer; the creation date for an account without any
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
