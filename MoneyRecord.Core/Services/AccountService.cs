using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;

namespace MoneyRecord.Services
{
    public sealed class AccountService : IAccountService
    {
        private readonly DatabaseInitializer _dbInitializer;
        private readonly IAccountRepository _accountRepository;

        public AccountService(DatabaseInitializer dbInitializer, IAccountRepository accountRepository)
        {
            _dbInitializer = dbInitializer;
            _accountRepository = accountRepository;
        }

        public async Task<AccountUsage> GetUsageAsync(int accountId)
        {
            var db = await _dbInitializer.GetConnectionAsync();

            return new AccountUsage(
                await db.Table<Transaction>().CountAsync(t => t.AccountId == accountId),
                await db.Table<Transfer>().CountAsync(t => t.SourceAccountId == accountId || t.DestinationAccountId == accountId),
                await db.Table<RecurringTransaction>().CountAsync(r => r.AccountId == accountId));
        }

        public async Task<AccountMove?> GetMoveAsync(int accountId)
        {
            var db = await _dbInitializer.GetConnectionAsync();
            var account = await db.FindAsync<Account>(accountId);
            var defaultAccount = await _accountRepository.GetDefaultAsync();
            if (account == null || defaultAccount == null)
                return null;

            var transactions = await db.Table<Transaction>().Where(t => t.AccountId == accountId).ToListAsync();
            return new AccountMove(
                transactions.Count,
                await db.Table<RecurringTransaction>().CountAsync(r => r.AccountId == accountId),
                transactions.Sum(NetCents),
                defaultAccount.Name,
                MoneyFormatter.ResolveCurrencyCode(account.CurrencyCode),
                MoneyFormatter.ResolveCurrencyCode(defaultAccount.CurrencyCode));
        }

        /// <summary>What a transaction adds to the account: income up, expense down (a transfer row counts nothing).</summary>
        private static long NetCents(Transaction t) => t.Type switch
        {
            TransactionType.Income => t.AmountCents,
            TransactionType.Expense => -t.AmountCents,
            _ => 0
        };

        public async Task DeleteAsync(int accountId, decimal? rate = null)
        {
            try
            {
                await _dbInitializer.RunInTransactionAsync(db =>
                {
                    var account = db.Find<Account>(accountId)
                        ?? throw new InvalidOperationException($"Account {accountId} does not exist.");
                    if (account.IsDefault)
                        throw new InvalidOperationException("The default account can't be deleted.");
                    if (account.IsDebt)
                        throw new InvalidOperationException("A debt's account goes with its debt (IDebtService.DeleteAsync).");

                    var transfers = db.Table<Transfer>()
                        .Count(t => t.SourceAccountId == accountId || t.DestinationAccountId == accountId);
                    if (transfers > 0)
                        throw new InvalidOperationException($"Account {accountId} still has {transfers} transfer(s).");

                    var defaultAccount = db.Table<Account>().FirstOrDefault(a => a.IsDefault)
                        ?? throw new InvalidOperationException("There is no default account to move transactions to.");

                    // Moving amounts into another currency would silently change what they are worth: only at a rate
                    var inUse = db.Table<Transaction>().Count(t => t.AccountId == accountId) > 0
                        || db.Table<RecurringTransaction>().Count(r => r.AccountId == accountId) > 0;
                    if (inUse && !HaveSameCurrency(account, defaultAccount))
                    {
                        if (rate is not > 0)
                            throw new InvalidOperationException(
                                $"Account {accountId} ({account.CurrencyCode}) has transactions; the default account is in {defaultAccount.CurrencyCode} and no rate was given.");

                        ConvertAmounts(db, accountId, rate.Value);
                    }

                    db.Execute("UPDATE \"Transaction\" SET AccountId = ? WHERE AccountId = ?", defaultAccount.Id, accountId);
                    db.Execute("UPDATE \"RecurringTransaction\" SET AccountId = ? WHERE AccountId = ?", defaultAccount.Id, accountId);
                    db.Execute("DELETE FROM \"QuickEntry\" WHERE AccountId = ?", accountId);
                    // Its goals keep their other accounts
                    db.Execute("DELETE FROM \"SavingsGoalAccount\" WHERE AccountId = ?", accountId);
                    // A challenge that saves into it goes back to only marking days
                    db.Execute("UPDATE \"SavingsChallenge\" SET ToAccountId = NULL, ToPocketId = NULL WHERE ToAccountId = ?", accountId);
                    // Its pockets go with it: their movements and goal links too
                    db.Execute("DELETE FROM \"PocketMovement\" WHERE PocketId IN (SELECT Id FROM \"Pocket\" WHERE AccountId = ?)", accountId);
                    db.Execute("DELETE FROM \"SavingsGoalPocket\" WHERE PocketId IN (SELECT Id FROM \"Pocket\" WHERE AccountId = ?)", accountId);
                    db.Execute("DELETE FROM \"Pocket\" WHERE AccountId = ?", accountId);
                    db.Delete<Account>(accountId);
                });
            }
            finally
            {
                _accountRepository.InvalidateCache();
            }
        }

        /// <summary>
        /// The account's transactions and recurring rules in the other currency: every amount times the rate. A
        /// transaction's own conversion (the "Amount in USD" of a statement) was for the old currency, so it is cleared.
        /// </summary>
        private static void ConvertAmounts(SQLite.SQLiteConnection db, int accountId, decimal rate)
        {
            var transactions = db.Table<Transaction>().Where(t => t.AccountId == accountId).ToList();
            foreach (var t in transactions)
            {
                t.AmountCents = AccountMoveRate.Convert(t.AmountCents, rate);
                t.ConvertedAmountCents = null;
                t.ConvertedCurrencyCode = null;
            }
            db.UpdateAll(transactions);

            var rules = db.Table<RecurringTransaction>().Where(r => r.AccountId == accountId).ToList();
            foreach (var r in rules)
            {
                r.AmountCents = AccountMoveRate.Convert(r.AmountCents, rate);
                r.InterestCents = AccountMoveRate.Convert(r.InterestCents, rate);
                r.LastAmountCents = AccountMoveRate.Convert(r.LastAmountCents, rate);
            }
            db.UpdateAll(rules);
        }

        /// <summary>Same currency, counting an empty currency code as the base currency.</summary>
        public static bool HaveSameCurrency(Account a, Account b) =>
            MoneyFormatter.ResolveCurrencyCode(a.CurrencyCode) == MoneyFormatter.ResolveCurrencyCode(b.CurrencyCode);
    }
}
