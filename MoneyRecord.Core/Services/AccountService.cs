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

        public async Task DeleteAsync(int accountId)
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

                    // Moving amounts into another currency would silently change what they are worth
                    var inUse = db.Table<Transaction>().Count(t => t.AccountId == accountId) > 0
                        || db.Table<RecurringTransaction>().Count(r => r.AccountId == accountId) > 0;
                    if (inUse && !HaveSameCurrency(account, defaultAccount))
                        throw new InvalidOperationException(
                            $"Account {accountId} ({account.CurrencyCode}) has transactions; the default account is in {defaultAccount.CurrencyCode}.");

                    db.Execute("UPDATE \"Transaction\" SET AccountId = ? WHERE AccountId = ?", defaultAccount.Id, accountId);
                    db.Execute("UPDATE \"RecurringTransaction\" SET AccountId = ? WHERE AccountId = ?", defaultAccount.Id, accountId);
                    // Its goals keep their other accounts
                    db.Execute("DELETE FROM \"SavingsGoalAccount\" WHERE AccountId = ?", accountId);
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

        /// <summary>Same currency, counting an empty currency code as the base currency.</summary>
        public static bool HaveSameCurrency(Account a, Account b) =>
            MoneyFormatter.ResolveCurrencyCode(a.CurrencyCode) == MoneyFormatter.ResolveCurrencyCode(b.CurrencyCode);
    }
}
