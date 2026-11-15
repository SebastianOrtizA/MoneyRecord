using System.Security.Cryptography;
using System.Text;
using MoneyRecord.Models;
using SQLite;

namespace MoneyRecord.Services.Repositories
{
    /// <summary>
    /// Notices every write to a synced table with SQLite triggers (so raw SQL in any service is caught, not only repositories):
    /// each insert, update and delete adds a <see cref="SyncDirty"/> row, which <see cref="SyncStateService"/> turns into
    /// <see cref="SyncMeta"/>. The triggers exist only while sync is on, so a user who never syncs pays nothing.
    /// </summary>
    public static class SyncCapture
    {
        /// <summary>The tables whose rows are synced.</summary>
        public static readonly IReadOnlyList<Type> SyncedTables =
        [
            typeof(Category), typeof(Transaction), typeof(Account), typeof(Transfer), typeof(Budget),
            typeof(RecurringTransaction), typeof(SavingsGoal), typeof(SavingsGoalAccount), typeof(Payee), typeof(Debt),
            typeof(Trip), typeof(TripCategory), typeof(TripBudget), typeof(Participant), typeof(PartyEvent),
            typeof(PartyMember), typeof(PartyExpense), typeof(PartyExpenseExclusion), typeof(PartyContribution),
            typeof(PartyPayment), typeof(Pocket), typeof(PocketMovement), typeof(SavingsGoalPocket),
            typeof(SavingsChallenge), typeof(SavingsChallengeDay), typeof(QuickEntry)
        ];

        /// <summary>
        /// Tables that are not synced: a cache (ExchangeRate), indexes rebuilt from a row's own text (TransactionTag), receipt
        /// links (TransactionAttachment: they come with the receipt files, a later stage) and the sync tables themselves.
        /// </summary>
        public static readonly IReadOnlyList<Type> ExcludedTables =
        [
            typeof(ExchangeRate), typeof(TransactionTag), typeof(TransactionAttachment),
            typeof(SyncMeta), typeof(SyncDirty), typeof(SyncPeerState), typeof(SyncState)
        ];

        private const string ApplyingTest = "(SELECT Applying FROM SyncState WHERE Id = 1) = 0";

        public static string TableName(Type type) => new TableMapping(type).TableName;

        /// <summary>The SQL of the three triggers of every synced table.</summary>
        public static IReadOnlyList<(string Name, string Sql)> Triggers(SQLiteConnection db)
        {
            var triggers = new List<(string, string)>();
            foreach (var type in SyncedTables)
            {
                var mapping = db.GetMapping(type);
                var table = mapping.TableName;
                var key = mapping.PK?.Name ?? throw new InvalidOperationException($"{table} has no primary key.");
                var json = string.Join(", ", mapping.Columns.Select(c => $"'{c.Name}', OLD.\"{c.Name}\""));

                triggers.Add(($"SyncInsert_{table}", Build($"SyncInsert_{table}", "INSERT", table, $"'{table}', NEW.\"{key}\", 0, NULL")));
                triggers.Add(($"SyncUpdate_{table}", Build($"SyncUpdate_{table}", "UPDATE", table, $"'{table}', NEW.\"{key}\", 0, NULL")));
                triggers.Add(($"SyncDelete_{table}", Build($"SyncDelete_{table}", "DELETE", table, $"'{table}', OLD.\"{key}\", 1, json_object({json})")));
            }
            return triggers;
        }

        private static string Build(string name, string action, string table, string values) =>
            $"CREATE TRIGGER \"{name}\" AFTER {action} ON \"{table}\" WHEN {ApplyingTest} " +
            $"BEGIN INSERT INTO SyncDirty (TableName, LocalId, Deleted, Payload) VALUES ({values}); END";

        public static string HashOf(IReadOnlyList<(string Name, string Sql)> triggers) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', triggers.Select(t => t.Sql)))));

        /// <summary>(Re)installs the triggers; returns their hash.</summary>
        public static string Install(SQLiteConnection db)
        {
            Remove(db);
            var triggers = Triggers(db);
            foreach (var (_, sql) in triggers)
                db.Execute(sql);
            return HashOf(triggers);
        }

        public static void Remove(SQLiteConnection db)
        {
            foreach (var name in db.QueryScalars<string>("SELECT name FROM sqlite_master WHERE type = 'trigger' AND name LIKE 'Sync%'"))
                db.Execute($"DROP TRIGGER \"{name}\"");
        }

        /// <summary>After a schema change (a new column) the triggers are rebuilt so the deleted-row payload has it.</summary>
        public static void EnsureCurrent(SQLiteConnection db)
        {
            var state = db.Find<SyncState>(1);
            if (state is not { Enabled: true } || state.TriggerHash == HashOf(Triggers(db)))
                return;
            state.TriggerHash = Install(db);
            db.Update(state);
        }

        /// <summary>
        /// A backup is being restored over this database: its rows are not changes to send, and the sync data belongs to the old
        /// content, so sync is turned off with a new device id (it is switched on again, and paired again, if wanted).
        /// Run inside the restore's transaction, before the rows are replaced.
        /// </summary>
        public static void ResetOnRestore(SQLiteConnection db)
        {
            Remove(db);
            db.DeleteAll<SyncDirty>();
            db.DeleteAll<SyncMeta>();
            db.DeleteAll<SyncPeerState>();
            db.InsertOrReplace(new SyncState());
        }
    }
}
