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
            Category? expenseCategory, Category? incomeCategory) =>
            await PreviewAsync(new ParsedStatement(table.Rows.Select((row, i) => ToEntry(row, i + 1, mapping)).ToList()),
                account, expenseCategory, incomeCategory);

        public async Task<List<ImportRow>> PreviewAsync(ParsedStatement statement, Account account,
            Category? expenseCategory, Category? incomeCategory)
        {
            // A file that names its currency must match the account's
            var otherCurrency = statement.CurrencyCode != null
                && !MoneyFormatter.SameCurrency(statement.CurrencyCode, account.CurrencyCode);
            var parsed = statement.Entries.Select(e => Parse(e, otherCurrency)).ToList();
            var learned = await LearnedCategoriesAsync();
            var readable = parsed.Where(p => p.Problem == ImportProblem.None).ToList();
            var recorded = await RecordedEntriesAsync(account.Id, readable);

            return parsed.Select(p =>
            {
                if (p.Problem != ImportProblem.None)
                {
                    return new ImportRow { Line = p.Line, Date = p.Date, Description = p.Description, Problem = p.Problem,
                        CurrencyCode = account.CurrencyCode };
                }

                var duplicate = recorded.IsRecorded(p);
                var category = learned.GetValueOrDefault((p.Type, CategoryKey(p.Description)));
                return new ImportRow
                {
                    Line = p.Line,
                    Date = p.Date,
                    Description = p.Description,
                    AmountCents = p.Cents,
                    Type = p.Type,
                    CurrencyCode = account.CurrencyCode,
                    ExternalId = p.ExternalId,
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
                    ExternalId = r.ExternalId,
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

        /// <summary>A CSV row as an entry: the amount's sign says expense or income (flipped when positive is an expense).</summary>
        private static StatementEntry ToEntry(string[] row, int line, StatementMapping mapping)
        {
            var description = row[mapping.DescriptionColumn].Trim();
            DateTime? date = mapping.DateFormat.TryParse(row[mapping.DateColumn], out var d) ? d : null;
            decimal? amount = StatementAmount.TryParse(row[mapping.AmountColumn], mapping.DecimalSeparator, out var a)
                ? (mapping.PositiveIsExpense ? -a : a)
                : null;
            return new StatementEntry(line, date, description, amount);
        }

        private static ParsedRow Parse(StatementEntry entry, bool otherCurrency)
        {
            var description = entry.Description;
            if (entry.Date is not { } date)
                return new ParsedRow(entry.Line, default, description, 0, default, null, ImportProblem.Date);
            if (entry.Amount is not { } amount || !Money.IsInRange(amount))
                return new ParsedRow(entry.Line, date, description, 0, default, null, ImportProblem.Amount);

            var cents = Money.ToCents(Math.Abs(amount));
            if (cents == 0)
                return new ParsedRow(entry.Line, date, description, 0, default, null, ImportProblem.ZeroAmount);
            if (otherCurrency)
                return new ParsedRow(entry.Line, date, description, 0, default, null, ImportProblem.Currency);

            return new ParsedRow(entry.Line, date, description, cents,
                amount < 0 ? TransactionType.Expense : TransactionType.Income, entry.ExternalId, ImportProblem.None);
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

        /// <summary>The account's entries in the rows' date range: by the bank's ID, and counted per <see cref="ParsedRow.DuplicateKey"/>.</summary>
        private async Task<Recorded> RecordedEntriesAsync(int accountId, List<ParsedRow> rows)
        {
            if (rows.Count == 0)
                return new Recorded([], [], []);

            var from = rows.Min(r => r.Date);
            var to = rows.Max(r => r.Date).AddDays(1);
            var connection = await database.GetConnectionAsync();
            var entries = await connection.Table<Transaction>()
                .Where(t => t.AccountId == accountId && t.Date >= from && t.Date < to)
                .ToListAsync();
            entries = entries.Where(t => t.Type != TransactionType.Transfer).ToList();

            // An entry imported with a bank ID is found by it, wherever it is dated now (the user may have moved it)
            var ids = rows.Select(r => r.ExternalId).OfType<string>().Distinct().ToList();
            var knownIds = new HashSet<string>();
            foreach (var chunk in ids.Chunk(400))
            {
                var found = await connection.QueryAsync<IdOnly>(
                    $"SELECT ExternalId FROM \"Transaction\" WHERE AccountId = ? AND ExternalId IN ({string.Join(',', chunk.Select(_ => "?"))})",
                    [accountId, .. chunk]);
                knownIds.UnionWith(found.Select(f => f.ExternalId));
            }

            return new Recorded(knownIds, Count(entries), Count(entries.Where(t => t.ExternalId == null)));

            // A split payment is one statement row: its parts count once, with their total and the first part's text
            static Dictionary<string, int> Count(IEnumerable<Transaction> entries) => entries
                .GroupBy(t => t.SplitId ?? $"#{t.Id}")
                .Select(payment => DuplicateKey(payment.First().Date, payment.First().Type,
                    payment.Sum(t => Math.Abs(t.AmountCents)), payment.OrderBy(t => t.Id).First().Description))
                .GroupBy(key => key)
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
            string? ExternalId, ImportProblem Problem)
        {
            public string DuplicateKey => StatementImportService.DuplicateKey(Date, Type, Cents, Description);
        }

        private sealed class IdOnly
        {
            public string ExternalId { get; set; } = string.Empty;
        }

        /// <summary>
        /// What the account already has. A row with a bank ID is a duplicate when that ID is recorded, else when an entry
        /// without an ID (typed in, or from a CSV) matches its day, type, amount and text; a row without one matches any entry.
        /// Each recorded entry is used up by one row ("two equal coffees in the file and one recorded" = one duplicate).
        /// </summary>
        private sealed class Recorded(HashSet<string> ids, Dictionary<string, int> all, Dictionary<string, int> withoutId)
        {
            public bool IsRecorded(ParsedRow row)
            {
                if (row.ExternalId != null && ids.Contains(row.ExternalId))
                    return true;

                var counts = row.ExternalId == null ? all : withoutId;
                if (!counts.TryGetValue(row.DuplicateKey, out var count) || count == 0)
                    return false;
                counts[row.DuplicateKey] = count - 1;
                return true;
            }
        }

        private sealed class DescriptionCategory
        {
            public TransactionType Type { get; set; }

            public string Description { get; set; } = string.Empty;

            public int CategoryId { get; set; }
        }
    }
}
