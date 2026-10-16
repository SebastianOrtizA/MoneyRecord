using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;
using System.Text;

namespace MoneyRecord.Services
{
    public sealed class StatementImportService(DatabaseInitializer database, ICategoryRepository categoryRepository)
        : IStatementImportService
    {
        public async Task<List<ImportRow>> PreviewAsync(CsvTable table, StatementMapping mapping, Account account,
            Category? expenseCategory, Category? incomeCategory)
        {
            var parsed = table.Rows.Select((row, i) => Parse(row, i + 1, mapping)).ToList();
            var learned = await LearnedCategoriesAsync();
            var recorded = await RecordedEntriesAsync(account.Id, parsed.Where(p => p.Problem == ImportProblem.None).ToList());

            return parsed.Select(p =>
            {
                if (p.Problem != ImportProblem.None)
                {
                    return new ImportRow { Line = p.Line, Date = p.Date, Description = p.Description, Problem = p.Problem,
                        CurrencyCode = account.CurrencyCode };
                }

                var duplicate = recorded.TryGetValue(p.DuplicateKey, out var count) && count > 0;
                if (duplicate)
                    recorded[p.DuplicateKey] = count - 1;

                var category = learned.GetValueOrDefault((p.Type, CategoryKey(p.Description)));
                return new ImportRow
                {
                    Line = p.Line,
                    Date = p.Date,
                    Description = p.Description,
                    AmountCents = p.Cents,
                    Type = p.Type,
                    CurrencyCode = account.CurrencyCode,
                    IsDuplicate = duplicate,
                    IsLearned = category != null,
                    IsIncluded = !duplicate,
                    Category = category ?? (p.Type == TransactionType.Expense ? expenseCategory : incomeCategory),
                };
            }).ToList();
        }

        public async Task<List<int>> ImportAsync(Account account, IEnumerable<ImportRow> rows)
        {
            var entries = rows
                .Where(r => r.IsIncluded && r.CanImport && r.Category != null)
                .Select(r => new Transaction
                {
                    Date = r.Date,
                    Description = r.Description,
                    AmountCents = r.AmountCents,
                    Type = r.Type,
                    CategoryId = r.Category!.Id,
                    AccountId = account.Id,
                })
                .ToList();

            await database.RunInTransactionAsync(db => db.InsertAll(entries, runInTransaction: false));
            return entries.Select(t => t.Id).ToList();
        }

        public async Task UndoAsync(IReadOnlyCollection<int> transactionIds) =>
            await database.RunInTransactionAsync(db =>
            {
                foreach (var id in transactionIds)
                    db.Delete<Transaction>(id);
            });

        private static ParsedRow Parse(string[] row, int line, StatementMapping mapping)
        {
            var description = row[mapping.DescriptionColumn].Trim();
            if (!mapping.DateFormat.TryParse(row[mapping.DateColumn], out var date))
                return new ParsedRow(line, default, description, 0, default, ImportProblem.Date);
            if (!StatementAmount.TryParse(row[mapping.AmountColumn], mapping.DecimalSeparator, out var amount)
                || !Money.IsInRange(amount))
                return new ParsedRow(line, date, description, 0, default, ImportProblem.Amount);

            var cents = Money.ToCents(Math.Abs(amount));
            if (cents == 0)
                return new ParsedRow(line, date, description, 0, default, ImportProblem.ZeroAmount);

            var isExpense = amount < 0 != mapping.PositiveIsExpense;
            return new ParsedRow(line, date, description, cents, isExpense ? TransactionType.Expense : TransactionType.Income,
                ImportProblem.None);
        }

        /// <summary>Per type and <see cref="CategoryKey"/>, the category of the latest entry (that still exists).</summary>
        private async Task<Dictionary<(TransactionType, string), Category>> LearnedCategoriesAsync()
        {
            var categories = (await categoryRepository.GetAllAsync()).ToDictionary(c => c.Id);
            var history = await (await database.GetConnectionAsync()).QueryAsync<DescriptionCategory>(
                "SELECT Type, Description, CategoryId FROM \"Transaction\" WHERE Type IN (?, ?) AND Description <> '' " +
                "ORDER BY Date, Id", TransactionType.Income, TransactionType.Expense);

            var learned = new Dictionary<(TransactionType, string), Category>();
            foreach (var entry in history)
            {
                var key = CategoryKey(entry.Description);
                if (key.Length > 0 && categories.TryGetValue(entry.CategoryId, out var category))
                    learned[(entry.Type, key)] = category; // later entries win
            }
            return learned;
        }

        /// <summary>How many entries the account has per <see cref="ParsedRow.DuplicateKey"/> in the rows' date range.</summary>
        private async Task<Dictionary<string, int>> RecordedEntriesAsync(int accountId, List<ParsedRow> rows)
        {
            if (rows.Count == 0)
                return [];

            var from = rows.Min(r => r.Date);
            var to = rows.Max(r => r.Date).AddDays(1);
            var entries = await (await database.GetConnectionAsync()).Table<Transaction>()
                .Where(t => t.AccountId == accountId && t.Date >= from && t.Date < to)
                .ToListAsync();

            return entries
                .Where(t => t.Type != TransactionType.Transfer)
                .GroupBy(t => DuplicateKey(t.Date, t.Type, Math.Abs(t.AmountCents), t.Description))
                .ToDictionary(g => g.Key, g => g.Count());
        }

        private static string DuplicateKey(DateTime date, TransactionType type, long cents, string description) =>
            $"{date:yyyyMMdd}|{type}|{cents}|{string.Join(' ', StatementMapping.Normalize(description).Split(' ', StringSplitOptions.RemoveEmptyEntries))}";

        /// <summary>The description's words without digits or punctuation: "COMPRA ÉXITO #1234" → "compra exito".</summary>
        internal static string CategoryKey(string description)
        {
            var words = new StringBuilder();
            foreach (var word in StatementMapping.Normalize(description).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var letters = new string(word.Where(char.IsLetter).ToArray());
                if (letters.Length == 0)
                    continue;
                if (words.Length > 0)
                    words.Append(' ');
                words.Append(letters);
            }
            return words.ToString();
        }

        private sealed record ParsedRow(int Line, DateTime Date, string Description, long Cents, TransactionType Type,
            ImportProblem Problem)
        {
            public string DuplicateKey => StatementImportService.DuplicateKey(Date, Type, Cents, Description);
        }

        private sealed class DescriptionCategory
        {
            public TransactionType Type { get; set; }

            public string Description { get; set; } = string.Empty;

            public int CategoryId { get; set; }
        }
    }
}
