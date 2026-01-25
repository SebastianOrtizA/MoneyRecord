using SQLite;
using MoneyRecord.Models;

namespace MoneyRecord.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection? _database;

        public async Task InitializeAsync()
        {
            if (_database != null)
                return;

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "moneyrecord.db3");
            _database = new SQLiteAsyncConnection(dbPath);

            await _database.CreateTableAsync<Category>();
            await _database.CreateTableAsync<Transaction>();

            // Add default categories if none exist
            var categoryCount = await _database.Table<Category>().CountAsync();
            if (categoryCount == 0)
            {
                await AddDefaultCategories();
            }
        }

        private async Task AddDefaultCategories()
        {
            var defaultIncomeCategories = new[]
            {
                new Category { Name = "Salary", Type = CategoryType.Income },
                new Category { Name = "Freelance", Type = CategoryType.Income },
                new Category { Name = "Investment", Type = CategoryType.Income },
                new Category { Name = "Other Income", Type = CategoryType.Income }
            };

            var defaultExpenseCategories = new[]
            {
                new Category { Name = "Food", Type = CategoryType.Expense },
                new Category { Name = "Transportation", Type = CategoryType.Expense },
                new Category { Name = "Entertainment", Type = CategoryType.Expense },
                new Category { Name = "Utilities", Type = CategoryType.Expense },
                new Category { Name = "Shopping", Type = CategoryType.Expense },
                new Category { Name = "Other Expense", Type = CategoryType.Expense }
            };

            foreach (var category in defaultIncomeCategories)
            {
                await _database!.InsertAsync(category);
            }

            foreach (var category in defaultExpenseCategories)
            {
                await _database!.InsertAsync(category);
            }
        }

        // Category operations
        public async Task<List<Category>> GetCategoriesAsync(CategoryType type)
        {
            await InitializeAsync();
            return await _database!.Table<Category>()
                .Where(c => c.Type == type)
                .ToListAsync();
        }

        public async Task<int> SaveCategoryAsync(Category category)
        {
            await InitializeAsync();
            if (category.Id != 0)
            {
                return await _database!.UpdateAsync(category);
            }
            else
            {
                return await _database!.InsertAsync(category);
            }
        }

        public async Task<int> DeleteCategoryAsync(Category category)
        {
            await InitializeAsync();
            return await _database!.DeleteAsync(category);
        }

        public async Task<bool> CategoryHasTransactionsAsync(int categoryId)
        {
            await InitializeAsync();
            
            try
            {
                var count = await _database!.Table<Transaction>()
                    .Where(t => t.CategoryId == categoryId)
                    .CountAsync();
                
                return count > 0;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<int> ReassignTransactionsCategoryAsync(int fromCategoryId, int toCategoryId)
        {
            await InitializeAsync();
            var transactions = await _database!.Table<Transaction>()
                .Where(t => t.CategoryId == fromCategoryId)
                .ToListAsync();

            foreach (var transaction in transactions)
            {
                transaction.CategoryId = toCategoryId;
                await _database.UpdateAsync(transaction);
            }

            return transactions.Count;
        }

        // Transaction operations
        public async Task<List<Transaction>> GetTransactionsAsync(DateTime startDate, DateTime endDate)
        {
            await InitializeAsync();
            var transactions = await _database!.Table<Transaction>()
                .Where(t => t.Date >= startDate && t.Date <= endDate)
                .ToListAsync();

            // Load category names
            foreach (var transaction in transactions)
            {
                var category = await _database.Table<Category>()
                    .Where(c => c.Id == transaction.CategoryId)
                    .FirstOrDefaultAsync();
                transaction.CategoryName = category?.Name ?? "Unknown";
            }

            return transactions;
        }

        public async Task<int> SaveTransactionAsync(Transaction transaction)
        {
            await InitializeAsync();
            if (transaction.Id != 0)
            {
                return await _database!.UpdateAsync(transaction);
            }
            else
            {
                return await _database!.InsertAsync(transaction);
            }
        }

        public async Task<int> DeleteTransactionAsync(Transaction transaction)
        {
            await InitializeAsync();
            return await _database!.DeleteAsync(transaction);
        }

        public async Task<decimal> GetBalanceAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            await InitializeAsync();
            
            var query = _database!.Table<Transaction>();
            
            if (startDate.HasValue && endDate.HasValue)
            {
                query = query.Where(t => t.Date >= startDate.Value && t.Date <= endDate.Value);
            }

            var transactions = await query.ToListAsync();

            var incomes = transactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
            var expenses = transactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);

            return incomes - expenses;
        }

        public async Task<decimal> GetTotalIncomesAsync(DateTime startDate, DateTime endDate)
        {
            await InitializeAsync();
            var transactions = await _database!.Table<Transaction>()
                .Where(t => t.Type == TransactionType.Income && t.Date >= startDate && t.Date <= endDate)
                .ToListAsync();

            return transactions.Sum(t => t.Amount);
        }

        public async Task<decimal> GetTotalExpensesAsync(DateTime startDate, DateTime endDate)
        {
            await InitializeAsync();
            var transactions = await _database!.Table<Transaction>()
                .Where(t => t.Type == TransactionType.Expense && t.Date >= startDate && t.Date <= endDate)
                .ToListAsync();

            return transactions.Sum(t => t.Amount);
        }

        // Debugging helper method
        public async Task<int> GetTransactionCountByCategoryAsync(int categoryId)
        {
            await InitializeAsync();
            return await _database!.Table<Transaction>()
                .Where(t => t.CategoryId == categoryId)
                .CountAsync();
        }
    }
}
