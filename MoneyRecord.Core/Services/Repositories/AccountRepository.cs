using MoneyRecord.Models;

namespace MoneyRecord.Services.Repositories
{
    /// <summary>Accounts are few and read on every screen, so they are cached until the next write.</summary>
    public sealed class AccountRepository(DatabaseInitializer database)
        : RepositoryBase<Account>(database), Interfaces.IAccountRepository
    {
        private List<Account>? _cache;

        public void InvalidateCache() => _cache = null;

        protected override void OnChanged() => InvalidateCache();

        public override async Task<List<Account>> GetAllAsync() => _cache ??= await base.GetAllAsync();

        public override async Task<Account?> GetByIdAsync(int id) =>
            (await GetAllAsync()).FirstOrDefault(a => a.Id == id);

        public async Task<Account?> GetDefaultAsync() =>
            (await GetAllAsync()).FirstOrDefault(a => a.IsDefault);

        public async Task<List<Account>> GetOwnAsync() => (await GetAllAsync()).Where(a => !a.IsDebt).ToList();
    }
}
