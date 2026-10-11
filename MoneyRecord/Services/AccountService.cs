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
            await _dbInitializer.InitializeAsync();
            var db = _dbInitializer.Database!;

            return new AccountUsage(
                await db.Table<Transaction>().CountAsync(t => t.AccountId == accountId),
                await db.Table<Transfer>().CountAsync(t => t.SourceAccountId == accountId || t.DestinationAccountId == accountId),
                await db.Table<RecurringTransaction>().CountAsync(r => r.AccountId == accountId));
        }

        public async Task DeleteAsync(int accountId)
        {
            await _dbInitializer.InitializeAsync();

            try
            {
                await _dbInitializer.Database!.RunInTransactionAsync(db =>
                {
                    var account = db.Find<Account>(accountId)
                        ?? throw new InvalidOperationException($"Account {accountId} does not exist.");
                    if (account.IsDefault)
                        throw new InvalidOperationException("The default account can't be deleted.");

                    var transfers = db.Table<Transfer>()
                        .Count(t => t.SourceAccountId == accountId || t.DestinationAccountId == accountId);
                    if (transfers > 0)
                        throw new InvalidOperationException($"Account {accountId} still has {transfers} transfer(s).");

                    var defaultAccount = db.Table<Account>().FirstOrDefault(a => a.IsDefault)
                        ?? throw new InvalidOperationException("There is no default account to move transactions to.");

                    db.Execute("UPDATE \"Transaction\" SET AccountId = ? WHERE AccountId = ?", defaultAccount.Id, accountId);
                    db.Execute("UPDATE \"RecurringTransaction\" SET AccountId = ? WHERE AccountId = ?", defaultAccount.Id, accountId);
                    db.Delete<Account>(accountId);
                });
            }
            finally
            {
                _accountRepository.InvalidateCache();
            }
        }
    }
}
