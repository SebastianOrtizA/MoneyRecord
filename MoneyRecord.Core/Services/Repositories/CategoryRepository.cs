using MoneyRecord.Models;

namespace MoneyRecord.Services.Repositories
{
    /// <summary>Categories are few and read on every screen, so they are cached until the next write.</summary>
    public sealed class CategoryRepository(DatabaseInitializer database)
        : RepositoryBase<Category>(database), Interfaces.ICategoryRepository
    {
        private List<Category>? _cache;
        private readonly Dictionary<CategoryType, List<Category>> _typeCache = new();

        public void InvalidateCache()
        {
            _cache = null;
            _typeCache.Clear();
        }

        protected override void OnChanged() => InvalidateCache();

        public override async Task<List<Category>> GetAllAsync() => _cache ??= await base.GetAllAsync();

        public override async Task<Category?> GetByIdAsync(int id) =>
            (await GetAllAsync()).FirstOrDefault(c => c.Id == id);

        public async Task<List<Category>> GetByTypeAsync(CategoryType type)
        {
            if (_typeCache.TryGetValue(type, out var cached))
                return cached;

            var filtered = (await GetAllAsync()).Where(c => c.Type == type).ToList();
            _typeCache[type] = filtered;
            return filtered;
        }
    }
}
