using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using System.Text.Json;

namespace MoneyRecord.Services
{
    /// <summary>
    /// Keeps the last <see cref="MaxLayouts"/> layouts' choices in preferences, as JSON. Columns are stored by position and
    /// the date layout by pattern, so they are checked against each new file before use.
    /// </summary>
    public sealed class StatementMappingStore(IPreferencesService preferences) : IStatementMappingStore
    {
        public const string PreferenceKey = "StatementMappings";

        /// <summary>One per bank layout; more than a person uses, the least recently used dropped.</summary>
        public const int MaxLayouts = 20;

        public SavedStatementMapping? Find(CsvTable table, StatementGuess guess)
        {
            var layout = Layout(table);
            var saved = Load().FirstOrDefault(e => e.Layout == layout);
            if (saved == null
                || !guess.DateFormats.TryGetValue(saved.DateColumn, out var formats)
                || formats.FirstOrDefault(f => f.Pattern == saved.DatePattern) is not { } format
                || !guess.AmountColumns.Contains(saved.AmountColumn))
                return null;

            var mapping = new StatementMapping(saved.DateColumn, format, saved.DescriptionColumn, saved.AmountColumn, '.',
                saved.PositiveIsExpense).WithAmountColumn(table, saved.AmountColumn);
            // Only a header names the bank: header-less files of two banks may have the same column count, so their columns
            // (checked above) are reused but not the account, where a wrong guess would go unnoticed
            return new SavedStatementMapping(mapping, table.HasHeader ? saved.AccountId : null);
        }

        public void Remember(CsvTable table, StatementMapping mapping, int accountId)
        {
            var layout = Layout(table);
            var entries = Load().Where(e => e.Layout != layout).ToList();
            entries.Insert(0, new Entry(layout, mapping.DateColumn, mapping.DateFormat.Pattern, mapping.DescriptionColumn,
                mapping.AmountColumn, mapping.PositiveIsExpense, accountId));
            preferences.Set(PreferenceKey, JsonSerializer.Serialize(entries.Take(MaxLayouts).ToList()));
        }

        /// <summary>
        /// The header names (trimmed, lower case, no accents), or only the column count for a file without a header: its
        /// "Column n" names depend on the app language.
        /// </summary>
        private static string Layout(CsvTable table) => table.HasHeader
            ? string.Join('|', table.Headers.Select(StatementMapping.Normalize))
            : $"#{table.Headers.Count}";

        private List<Entry> Load()
        {
            var json = preferences.Get(PreferenceKey, string.Empty);
            if (json.Length == 0)
                return [];
            try
            {
                return JsonSerializer.Deserialize<List<Entry>>(json) ?? [];
            }
            catch (JsonException)
            {
                return []; // unreadable: start over rather than fail the import screen
            }
        }

        private sealed record Entry(string Layout, int DateColumn, string DatePattern, int DescriptionColumn, int AmountColumn,
            bool PositiveIsExpense, int AccountId);
    }
}
