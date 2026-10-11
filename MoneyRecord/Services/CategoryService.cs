using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;

namespace MoneyRecord.Services
{
    public sealed class CategoryService : ICategoryService
    {
        private readonly DatabaseInitializer _dbInitializer;
        private readonly ICategoryRepository _categoryRepository;

        public CategoryService(DatabaseInitializer dbInitializer, ICategoryRepository categoryRepository)
        {
            _dbInitializer = dbInitializer;
            _categoryRepository = categoryRepository;
        }

        public async Task<CategoryUsage> GetUsageAsync(int categoryId)
        {
            await _dbInitializer.InitializeAsync();
            var db = _dbInitializer.Database!;

            return new CategoryUsage(
                await db.Table<Transaction>().CountAsync(t => t.CategoryId == categoryId),
                await db.Table<RecurringTransaction>().CountAsync(r => r.CategoryId == categoryId),
                await db.Table<Budget>().CountAsync(b => b.CategoryId == categoryId));
        }

        public async Task<CategoryDeleteResult> DeleteAsync(int categoryId, int? replacementId)
        {
            await _dbInitializer.InitializeAsync();
            CategoryDeleteResult? result = null;

            try
            {
                await _dbInitializer.Database!.RunInTransactionAsync(db =>
                {
                    var category = db.Find<Category>(categoryId)
                        ?? throw new InvalidOperationException($"Category {categoryId} does not exist.");

                    var transactions = db.Table<Transaction>().Count(t => t.CategoryId == categoryId);
                    var recurringRules = db.Table<RecurringTransaction>().Count(r => r.CategoryId == categoryId);
                    var budgets = db.Table<Budget>().Count(b => b.CategoryId == categoryId);

                    if (replacementId is not { } newId)
                    {
                        if (transactions > 0 || recurringRules > 0)
                            throw new InvalidOperationException($"Category {categoryId} is in use and needs a replacement.");

                        db.Execute("DELETE FROM \"Budget\" WHERE CategoryId = ?", categoryId);
                        db.Delete<Category>(categoryId);
                        result = new CategoryDeleteResult(0, 0, 0, budgets);
                        return;
                    }

                    var replacement = db.Find<Category>(newId);
                    if (newId == categoryId || replacement == null || replacement.Type != category.Type)
                        throw new InvalidOperationException($"Category {newId} can't replace category {categoryId}.");

                    db.Execute("UPDATE \"Transaction\" SET CategoryId = ? WHERE CategoryId = ?", newId, categoryId);
                    db.Execute("UPDATE \"RecurringTransaction\" SET CategoryId = ? WHERE CategoryId = ?", newId, categoryId);

                    // One budget per category: the replacement keeps its own budget if it has one
                    var replacementHasBudget = db.Table<Budget>().Count(b => b.CategoryId == newId) > 0;
                    if (replacementHasBudget)
                        db.Execute("DELETE FROM \"Budget\" WHERE CategoryId = ?", categoryId);
                    else
                        db.Execute("UPDATE \"Budget\" SET CategoryId = ? WHERE CategoryId = ?", newId, categoryId);

                    db.Delete<Category>(categoryId);
                    result = replacementHasBudget
                        ? new CategoryDeleteResult(transactions, recurringRules, 0, budgets)
                        : new CategoryDeleteResult(transactions, recurringRules, budgets, 0);
                });
            }
            finally
            {
                _categoryRepository.InvalidateCache();
            }

            return result!;
        }
    }
}
