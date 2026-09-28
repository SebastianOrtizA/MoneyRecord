using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services.Repositories
{
    public sealed class CategoryRepository : ICategoryRepository
    {
        private readonly DatabaseInitializer _dbInitializer;
        private List<Category>? _allCache;
        private readonly Dictionary<CategoryType, List<Category>> _typeCache = new();

        public CategoryRepository(DatabaseInitializer dbInitializer)
        {
            _dbInitializer = dbInitializer;
        }

        private async Task EnsureInitializedAsync()
        {
            await _dbInitializer.InitializeAsync();
        }

        private void InvalidateCache()
        {
            _allCache = null;
            _typeCache.Clear();
        }

        public async Task<List<Category>> GetAllAsync()
        {
            if (_allCache != null)
                return _allCache;

            await EnsureInitializedAsync();
            _allCache = await _dbInitializer.Database!.Table<Category>().ToListAsync();
            return _allCache;
        }

        public async Task<Category?> GetByIdAsync(int id)
        {
            var all = await GetAllAsync();
            return all.FirstOrDefault(c => c.Id == id);
        }

        public async Task<List<Category>> GetByTypeAsync(CategoryType type)
        {
            if (_typeCache.TryGetValue(type, out var cached))
                return cached;

            var all = await GetAllAsync();
            var filtered = all.Where(c => c.Type == type).ToList();
            _typeCache[type] = filtered;
            return filtered;
        }

        public async Task<int> SaveAsync(Category entity)
        {
            await EnsureInitializedAsync();
            int result;
            if (entity.Id != 0)
            {
                result = await _dbInitializer.Database!.UpdateAsync(entity);
            }
            else
            {
                result = await _dbInitializer.Database!.InsertAsync(entity);
            }
            InvalidateCache();
            return result;
        }

        public async Task<int> DeleteAsync(Category entity)
        {
            await EnsureInitializedAsync();
            var result = await _dbInitializer.Database!.DeleteAsync(entity);
            InvalidateCache();
            return result;
        }

        public async Task<bool> HasTransactionsAsync(int categoryId)
        {
            await EnsureInitializedAsync();
            try
            {
                var count = await _dbInitializer.Database!.Table<Transaction>()
                    .Where(t => t.CategoryId == categoryId)
                    .CountAsync();
                return count > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<int> GetTransactionCountAsync(int categoryId)
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.Table<Transaction>()
                .Where(t => t.CategoryId == categoryId)
                .CountAsync();
        }
    }
}
