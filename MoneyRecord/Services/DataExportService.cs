using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace MoneyRecord.Services
{
    public sealed class DataExportService : IDataExportService
    {
        private readonly DatabaseInitializer _dbInitializer;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public DataExportService(DatabaseInitializer dbInitializer)
        {
            _dbInitializer = dbInitializer;
        }

        private async Task EnsureInitializedAsync()
        {
            await _dbInitializer.InitializeAsync();
        }

        public async Task<string> ExportJsonAsync()
        {
            await EnsureInitializedAsync();
            var db = _dbInitializer.Database!;

            var backup = new BackupData
            {
                ExportDate = DateTime.Now,
                Version = 1,
                Categories = await db.Table<Category>().ToListAsync(),
                Accounts = await db.Table<Account>().ToListAsync(),
                Transactions = await db.Table<Transaction>().ToListAsync(),
                Transfers = await db.Table<Transfer>().ToListAsync(),
                Budgets = await db.Table<Budget>().ToListAsync(),
                RecurringTransactions = await db.Table<RecurringTransaction>().ToListAsync()
            };

            return JsonSerializer.Serialize(backup, JsonOptions);
        }

        public async Task<string> ExportCsvAsync()
        {
            await EnsureInitializedAsync();
            var db = _dbInitializer.Database!;

            var transactions = await db.Table<Transaction>().ToListAsync();
            var categories = (await db.Table<Category>().ToListAsync()).ToDictionary(c => c.Id);
            var accounts = (await db.Table<Account>().ToListAsync()).ToDictionary(a => a.Id);

            var sb = new StringBuilder();
            sb.AppendLine("Date,Description,Amount,Type,Category,Account");

            foreach (var t in transactions.OrderByDescending(t => t.Date))
            {
                var categoryName = categories.TryGetValue(t.CategoryId, out var cat) ? cat.Name : "";
                var accountName = t.AccountId.HasValue && accounts.TryGetValue(t.AccountId.Value, out var acc) ? acc.Name : "";

                sb.Append(t.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.Append(CsvEscape(t.Description));
                sb.Append(',');
                sb.Append(t.Amount.ToString("F2", CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.Append(t.Type);
                sb.Append(',');
                sb.Append(CsvEscape(categoryName));
                sb.Append(',');
                sb.AppendLine(CsvEscape(accountName));
            }

            return sb.ToString();
        }

        public async Task<int> ImportJsonAsync(string json)
        {
            var backup = JsonSerializer.Deserialize<BackupData>(json, JsonOptions);
            if (backup == null)
                throw new InvalidOperationException("Invalid backup file");

            await EnsureInitializedAsync();
            var db = _dbInitializer.Database!;

            await db.RunInTransactionAsync(conn =>
            {
                conn.DeleteAll<RecurringTransaction>();
                conn.DeleteAll<Budget>();
                conn.DeleteAll<Transfer>();
                conn.DeleteAll<Transaction>();
                conn.DeleteAll<Account>();
                conn.DeleteAll<Category>();
            });

            int count = 0;

            foreach (var c in backup.Categories)
            {
                c.Id = 0;
                await db.InsertAsync(c);
                count++;
            }

            var accountIdMap = new Dictionary<int, int>();
            foreach (var a in backup.Accounts)
            {
                var oldId = a.Id;
                a.Id = 0;
                await db.InsertAsync(a);
                accountIdMap[oldId] = a.Id;
                count++;
            }

            var categoryIdMap = new Dictionary<int, int>();
            var insertedCategories = await db.Table<Category>().ToListAsync();
            foreach (var bc in backup.Categories)
            {
                var matched = insertedCategories.FirstOrDefault(c => c.Name == bc.Name && c.Type == bc.Type);
                if (matched != null)
                    categoryIdMap[bc.Id == 0 ? matched.Id : bc.Id] = matched.Id;
            }

            foreach (var t in backup.Transactions)
            {
                t.Id = 0;
                if (t.AccountId.HasValue && accountIdMap.TryGetValue(t.AccountId.Value, out var newAccId))
                    t.AccountId = newAccId;
                if (categoryIdMap.TryGetValue(t.CategoryId, out var newCatId))
                    t.CategoryId = newCatId;
                await db.InsertAsync(t);
                count++;
            }

            foreach (var tr in backup.Transfers)
            {
                tr.Id = 0;
                if (accountIdMap.TryGetValue(tr.SourceAccountId, out var newSrc))
                    tr.SourceAccountId = newSrc;
                if (accountIdMap.TryGetValue(tr.DestinationAccountId, out var newDst))
                    tr.DestinationAccountId = newDst;
                await db.InsertAsync(tr);
                count++;
            }

            foreach (var b in backup.Budgets)
            {
                b.Id = 0;
                if (categoryIdMap.TryGetValue(b.CategoryId, out var newCatId))
                    b.CategoryId = newCatId;
                await db.InsertAsync(b);
                count++;
            }

            foreach (var r in backup.RecurringTransactions)
            {
                r.Id = 0;
                if (accountIdMap.TryGetValue(r.AccountId, out var newAccId))
                    r.AccountId = newAccId;
                if (categoryIdMap.TryGetValue(r.CategoryId, out var newCatId))
                    r.CategoryId = newCatId;
                await db.InsertAsync(r);
                count++;
            }

            return count;
        }

        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }

    internal class BackupData
    {
        public DateTime ExportDate { get; set; }
        public int Version { get; set; }
        public List<Category> Categories { get; set; } = [];
        public List<Account> Accounts { get; set; } = [];
        public List<Transaction> Transactions { get; set; } = [];
        public List<Transfer> Transfers { get; set; } = [];
        public List<Budget> Budgets { get; set; } = [];
        public List<RecurringTransaction> RecurringTransactions { get; set; } = [];
    }
}
