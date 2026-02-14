using SQLite;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection? _database;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private bool _isInitialized;

        public async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            await _initLock.WaitAsync();
            try
            {
                if (_isInitialized)
                    return;

                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "moneyrecord.db3");
                _database = new SQLiteAsyncConnection(dbPath);

                await _database.CreateTableAsync<Category>();
                await _database.CreateTableAsync<Transaction>();
                await _database.CreateTableAsync<Account>();
                await _database.CreateTableAsync<Transfer>();

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

                _isInitialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        private async Task AddDefaultCategories()
        {
            var defaultIncomeCategories = new[]
            {
                new Category { Name = AppResources.DefaultCategorySalary, Type = CategoryType.Income, IconCode = "F0116" },       // cash-multiple
                new Category { Name = AppResources.DefaultCategoryFreelance, Type = CategoryType.Income, IconCode = "F00D6" },    // briefcase
                new Category { Name = AppResources.DefaultCategoryInvestment, Type = CategoryType.Income, IconCode = "F081F" },   // finance
                new Category { Name = AppResources.DefaultCategoryOtherIncome, Type = CategoryType.Income, IconCode = "F0CF4" }  // cash-register
            };

            var defaultExpenseCategories = new[]
            {
                new Category { Name = AppResources.DefaultCategoryFood, Type = CategoryType.Expense, IconCode = "F025A" },             // food
                new Category { Name = AppResources.DefaultCategoryTransportation, Type = CategoryType.Expense, IconCode = "F0BD8" },   // train-car
                new Category { Name = AppResources.DefaultCategoryEntertainment, Type = CategoryType.Expense, IconCode = "F0356" },    // glass-cocktail
                new Category { Name = AppResources.DefaultCategoryUtilities, Type = CategoryType.Expense, IconCode = "F0D15" },        // home-city
                new Category { Name = AppResources.DefaultCategoryShopping, Type = CategoryType.Expense, IconCode = "F0110" },         // cart
                new Category { Name = AppResources.DefaultCategoryHome, Type = CategoryType.Expense, IconCode = "F0D15" },             // home-city
                new Category { Name = AppResources.DefaultCategoryOtherExpense, Type = CategoryType.Expense, IconCode = "F0076" }     // basket
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
            catch
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

            // Load all data in parallel to avoid N+1 queries
            var transactionsTask = _database!.Table<Transaction>()
                .Where(t => t.Date >= startDate && t.Date <= endDate)
                .ToListAsync();
            var categoriesTask = _database.Table<Category>().ToListAsync();
            var accountsTask = _database.Table<Account>().ToListAsync();

            await Task.WhenAll(transactionsTask, categoriesTask, accountsTask);

            var transactions = await transactionsTask ?? new List<Transaction>();
            var categories = (await categoriesTask ?? new List<Category>()).ToDictionary(c => c.Id);
            var accounts = (await accountsTask ?? new List<Account>()).ToDictionary(a => a.Id);

            // Map relationships in memory (much faster than individual queries)
            foreach (var transaction in transactions)
            {
                if (categories.TryGetValue(transaction.CategoryId, out var category))
                {
                    transaction.CategoryName = category.Name ?? "Unknown";
                    transaction.CategoryIconCode = category.IconCode ?? "F0770";
                }
                else
                {
                    transaction.CategoryName = "Unknown";
                    transaction.CategoryIconCode = "F0770";
                }

                if (transaction.AccountId.HasValue && accounts.TryGetValue(transaction.AccountId.Value, out var account))
                {
                    transaction.AccountName = account.Name ?? "Cash";
                    transaction.AccountIconCode = account.IconCode ?? "F0070";
                }
                else
                {
                    transaction.AccountName = "Cash";
                    transaction.AccountIconCode = "F0115"; // cash-100
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

            // Use Math.Abs to handle cases where amounts might be stored as negative values
            return transactions.Sum(t => Math.Abs(t.Amount));
        }

        public async Task<decimal> GetTotalExpensesAsync(DateTime startDate, DateTime endDate)
        {
            await InitializeAsync();
            var transactions = await _database!.Table<Transaction>()
                .Where(t => t.Type == TransactionType.Expense && t.Date >= startDate && t.Date <= endDate)
                .ToListAsync();

            // Use Math.Abs to handle cases where amounts might be stored as negative values
            return transactions.Sum(t => Math.Abs(t.Amount));
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
                Name = AppResources.DefaultAccountCash,
                InitialBalance = 0,
                IsDefault = true,
                IconCode = "F0115" // cash-100 icon
            };
            await _database!.InsertAsync(cashAccount);
        }

        public async Task<List<Account>> GetAccountsAsync()
        {
            await InitializeAsync();
            return await _database!.Table<Account>().ToListAsync() ?? new List<Account>();
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

            // Use Math.Abs to handle cases where amounts might be stored as negative values
            var incomes = transactions
                .Where(t => t.Type == TransactionType.Income)
                .Sum(t => Math.Abs(t.Amount));
            var expenses = transactions
                .Where(t => t.Type == TransactionType.Expense)
                .Sum(t => Math.Abs(t.Amount));

            // Include transfers: outgoing transfers reduce balance, incoming transfers increase balance
            var outgoingTransfers = await _database.Table<Transfer>()
                .Where(t => t.SourceAccountId == accountId)
                .ToListAsync();
            var incomingTransfers = await _database.Table<Transfer>()
                .Where(t => t.DestinationAccountId == accountId)
                .ToListAsync();

            var transfersOut = outgoingTransfers.Sum(t => Math.Abs(t.Amount));
            var transfersIn = incomingTransfers.Sum(t => Math.Abs(t.Amount));

            return account.InitialBalance + incomes - expenses - transfersOut + transfersIn;
        }

        public async Task<decimal> GetTotalBalanceAsync()
        {
            await InitializeAsync();
            
            // Get sum of all account initial balances
            var accounts = await GetAccountsAsync() ?? new List<Account>();
            decimal totalInitialBalance = accounts.Sum(a => a.InitialBalance);

            // Get ALL transactions (including those without AccountId)
            var allTransactions = await _database!.Table<Transaction>().ToListAsync();
            
            // Use Math.Abs to handle cases where amounts might be stored as negative values
            var totalIncomes = allTransactions
                .Where(t => t.Type == TransactionType.Income)
                .Sum(t => Math.Abs(t.Amount));
            var totalExpenses = allTransactions
                .Where(t => t.Type == TransactionType.Expense)
                .Sum(t => Math.Abs(t.Amount));

            // Transfers don't affect total balance (money moves between accounts)
            // Balance = InitialBalance + Incomes - Expenses
            return totalInitialBalance + totalIncomes - totalExpenses;
        }

        public async Task<List<AccountBalanceInfo>> GetAllAccountBalancesAsync()
        {
            await InitializeAsync();
            
            var accounts = await GetAccountsAsync() ?? new List<Account>();
            var result = new List<AccountBalanceInfo>();

            foreach (var account in accounts)
            {
                var balance = await GetAccountBalanceAsync(account.Id);
                
                // Get the last transaction date for this account
                var lastTransaction = await _database!.Table<Transaction>()
                    .Where(t => t.AccountId == account.Id)
                    .OrderByDescending(t => t.Date)
                    .FirstOrDefaultAsync();

                // Get the last transfer date for this account (as source or destination)
                var lastTransferAsSource = await _database!.Table<Transfer>()
                    .Where(t => t.SourceAccountId == account.Id)
                    .OrderByDescending(t => t.Date)
                    .FirstOrDefaultAsync();

                var lastTransferAsDest = await _database!.Table<Transfer>()
                    .Where(t => t.DestinationAccountId == account.Id)
                    .OrderByDescending(t => t.Date)
                    .FirstOrDefaultAsync();

                // Determine the most recent activity date
                DateTime? lastActivityDate = null;

                // Collect all activity dates
                var activityDates = new List<DateTime>();
                
                if (lastTransaction != null)
                    activityDates.Add(lastTransaction.Date);
                
                if (lastTransferAsSource != null)
                    activityDates.Add(lastTransferAsSource.Date);
                
                if (lastTransferAsDest != null)
                    activityDates.Add(lastTransferAsDest.Date);

                if (activityDates.Count > 0)
                {
                    // Use the most recent transaction or transfer date
                    lastActivityDate = activityDates.Max();
                }
                else
                {
                    // Fallback to account's created date (when initial balance was assigned)
                    lastActivityDate = account.CreatedDate;
                }

                result.Add(new AccountBalanceInfo
                {
                    AccountId = account.Id,
                    AccountName = account.Name ?? string.Empty,
                    CurrentBalance = balance,
                    LastActivityDate = lastActivityDate
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

        // Transfer operations
        public async Task<List<Transfer>> GetTransfersAsync()
        {
            await InitializeAsync();
            var transfers = await _database!.Table<Transfer>().ToListAsync() ?? new List<Transfer>();

            // Load account names
            foreach (var transfer in transfers)
            {
                var sourceAccount = await _database.Table<Account>()
                    .Where(a => a.Id == transfer.SourceAccountId)
                    .FirstOrDefaultAsync();
                transfer.SourceAccountName = sourceAccount?.Name ?? "Unknown";

                var destAccount = await _database.Table<Account>()
                    .Where(a => a.Id == transfer.DestinationAccountId)
                    .FirstOrDefaultAsync();
                transfer.DestinationAccountName = destAccount?.Name ?? "Unknown";
            }

            return transfers;
        }

        public async Task<Transfer?> GetTransferAsync(int id)
        {
            await InitializeAsync();
            var transfer = await _database!.Table<Transfer>()
                .Where(t => t.Id == id)
                .FirstOrDefaultAsync();

            if (transfer != null)
            {
                var sourceAccount = await _database.Table<Account>()
                    .Where(a => a.Id == transfer.SourceAccountId)
                    .FirstOrDefaultAsync();
                transfer.SourceAccountName = sourceAccount?.Name ?? "Unknown";

                var destAccount = await _database.Table<Account>()
                    .Where(a => a.Id == transfer.DestinationAccountId)
                    .FirstOrDefaultAsync();
                transfer.DestinationAccountName = destAccount?.Name ?? "Unknown";
            }

            return transfer;
        }

        public async Task<int> SaveTransferAsync(Transfer transfer)
        {
            await InitializeAsync();
            if (transfer.Id != 0)
            {
                return await _database!.UpdateAsync(transfer);
            }
            else
            {
                return await _database!.InsertAsync(transfer);
            }
        }

        public async Task<int> DeleteTransferAsync(Transfer transfer)
        {
            await InitializeAsync();
            return await _database!.DeleteAsync(transfer);
        }

        public async Task<bool> AccountHasTransfersAsync(int accountId)
        {
            await InitializeAsync();
            var count = await _database!.Table<Transfer>()
                .Where(t => t.SourceAccountId == accountId || t.DestinationAccountId == accountId)
                .CountAsync();
            return count > 0;
        }

        /// <summary>
        /// Gets transfers within the specified date range with account names populated
        /// </summary>
        public async Task<List<Transfer>> GetTransfersAsync(DateTime startDate, DateTime endDate)
        {
            await InitializeAsync();
            var transfers = await _database!.Table<Transfer>()
                .Where(t => t.Date >= startDate && t.Date <= endDate)
                .ToListAsync() ?? new List<Transfer>();

            // Load account names
            foreach (var transfer in transfers)
            {
                var sourceAccount = await _database.Table<Account>()
                    .Where(a => a.Id == transfer.SourceAccountId)
                    .FirstOrDefaultAsync();
                transfer.SourceAccountName = sourceAccount?.Name ?? "Unknown";

                var destAccount = await _database.Table<Account>()
                    .Where(a => a.Id == transfer.DestinationAccountId)
                    .FirstOrDefaultAsync();
                transfer.DestinationAccountName = destAccount?.Name ?? "Unknown";
            }

            return transfers;
        }
    }
}
