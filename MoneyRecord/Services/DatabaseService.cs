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
            await _database.CreateTableAsync<Account>();

            // Add default categories if none exist
            var categoryCount = await _database.Table<Category>().CountAsync();
            if (categoryCount == 0)
            {
                await AddDefaultCategories();
            }

            // Add default "Cash" account if none exist
            var accountCount = await _database.Table<Account>().CountAsync();
            if (accountCount == 0)
            {
                await AddDefaultAccountAsync();
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

            // Load category names and account names
            foreach (var transaction in transactions)
            {
                var category = await _database.Table<Category>()
                    .Where(c => c.Id == transaction.CategoryId)
                    .FirstOrDefaultAsync();
                transaction.CategoryName = category?.Name ?? "Unknown";

                if (transaction.AccountId.HasValue)
                {
                    var account = await _database.Table<Account>()
                        .Where(a => a.Id == transaction.AccountId.Value)
                        .FirstOrDefaultAsync();
                    transaction.AccountName = account?.Name ?? "Cash";
                }
                else
                {
                    transaction.AccountName = "Cash";
                }
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

        // Account operations
        private async Task AddDefaultAccountAsync()
        {
            var cashAccount = new Account
            {
                Name = "Cash",
                InitialBalance = 0,
                IsDefault = true
            };
            await _database!.InsertAsync(cashAccount);
        }

        public async Task<List<Account>> GetAccountsAsync()
        {
            await InitializeAsync();
            return await _database!.Table<Account>().ToListAsync();
        }

        public async Task<Account?> GetAccountAsync(int id)
        {
            await InitializeAsync();
            return await _database!.Table<Account>()
                .Where(a => a.Id == id)
                .FirstOrDefaultAsync();
        }

        public async Task<Account?> GetDefaultAccountAsync()
        {
            await InitializeAsync();
            return await _database!.Table<Account>()
                .Where(a => a.IsDefault)
                .FirstOrDefaultAsync();
        }

        public async Task<int> SaveAccountAsync(Account account)
        {
            await InitializeAsync();
            if (account.Id != 0)
            {
                return await _database!.UpdateAsync(account);
            }
            else
            {
                return await _database!.InsertAsync(account);
            }
        }

        public async Task<int> DeleteAccountAsync(Account account)
        {
            await InitializeAsync();
            
            // Reassign transactions from this account to General account
            var defaultAccount = await GetDefaultAccountAsync();
            if (defaultAccount != null && account.Id != defaultAccount.Id)
            {
                var transactions = await _database!.Table<Transaction>()
                    .Where(t => t.AccountId == account.Id)
                    .ToListAsync();

                foreach (var transaction in transactions)
                {
                    transaction.AccountId = defaultAccount.Id;
                    await _database.UpdateAsync(transaction);
                }
            }

            return await _database!.DeleteAsync(account);
        }

        public async Task<bool> AccountHasTransactionsAsync(int accountId)
        {
            await InitializeAsync();
            var count = await _database!.Table<Transaction>()
                .Where(t => t.AccountId == accountId)
                .CountAsync();
            return count > 0;
        }

        public async Task<decimal> GetAccountBalanceAsync(int accountId)
        {
            await InitializeAsync();
            
            var account = await GetAccountAsync(accountId);
            if (account == null)
                return 0;

            var transactions = await _database!.Table<Transaction>()
                .Where(t => t.AccountId == accountId)
                .ToListAsync();

            var incomes = transactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
            var expenses = transactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);

            return account.InitialBalance + incomes - expenses;
        }

        public async Task<decimal> GetTotalBalanceAsync()
        {
            await InitializeAsync();
            
            var accounts = await GetAccountsAsync();
            decimal totalBalance = 0;

            foreach (var account in accounts)
            {
                totalBalance += await GetAccountBalanceAsync(account.Id);
            }

            return totalBalance;
        }

        public async Task<List<AccountBalanceInfo>> GetAllAccountBalancesAsync()
        {
            await InitializeAsync();
            
            var accounts = await GetAccountsAsync();
            var result = new List<AccountBalanceInfo>();

            foreach (var account in accounts)
            {
                var balance = await GetAccountBalanceAsync(account.Id);
                
                var lastTransaction = await _database!.Table<Transaction>()
                    .Where(t => t.AccountId == account.Id)
                    .OrderByDescending(t => t.Date)
                    .FirstOrDefaultAsync();

                result.Add(new AccountBalanceInfo
                {
                    AccountId = account.Id,
                    AccountName = account.Name,
                    CurrentBalance = balance,
                    LastTransactionDate = lastTransaction?.Date
                });
            }

            return result;
        }

        public async Task<DateTime?> GetLastTransactionDateForAccountAsync(int accountId)
        {
            await InitializeAsync();
            
            var lastTransaction = await _database!.Table<Transaction>()
                .Where(t => t.AccountId == accountId)
                .OrderByDescending(t => t.Date)
                .FirstOrDefaultAsync();

            return lastTransaction?.Date;
        }
    }
}
