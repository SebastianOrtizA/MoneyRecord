using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services.Repositories
{
    /// <summary>
    /// SQLite implementation of Category repository.
    /// Single responsibility: Category CRUD operations.
    /// </summary>
    public sealed class CategoryRepository : ICategoryRepository
    {
        private readonly DatabaseInitializer _dbInitializer;

        public CategoryRepository(DatabaseInitializer dbInitializer)
        {
            _dbInitializer = dbInitializer;
        }

        private async Task EnsureInitializedAsync()
        {
            await _dbInitializer.InitializeAsync();
        }

        public async Task<List<Category>> GetAllAsync()
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.Table<Category>().ToListAsync();
        }

        public async Task<Category?> GetByIdAsync(int id)
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.Table<Category>()
                .Where(c => c.Id == id)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Category>> GetByTypeAsync(CategoryType type)
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.Table<Category>()
                .Where(c => c.Type == type)
                .ToListAsync();
        }

        public async Task<int> SaveAsync(Category entity)
        {
            await EnsureInitializedAsync();
            if (entity.Id != 0)
            {
                return await _dbInitializer.Database!.UpdateAsync(entity);
            }
            return await _dbInitializer.Database!.InsertAsync(entity);
        }

        public async Task<int> DeleteAsync(Category entity)
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.DeleteAsync(entity);
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
