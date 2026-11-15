using MoneyRecord.Models;
using MoneyRecord.Services.Repositories;
using SQLite;

namespace MoneyRecord.Services.Sync
{
    /// <summary>
    /// What the sync layer must know about the tables: which integer columns point at another table (the local IDs travel as global
    /// ids), and the order to write them in (parents first). A test fails when a model gets an ID column that is not listed here.
    /// </summary>
    public static class SyncSchema
    {
        private static Dictionary<string, string> Fk(params (string Column, string Table)[] keys) =>
            keys.ToDictionary(k => k.Column, k => k.Table);

        /// <summary>Table to (column to the table it points at).</summary>
        public static readonly IReadOnlyDictionary<string, Dictionary<string, string>> ForeignKeys =
            new Dictionary<string, Dictionary<string, string>>
            {
                [nameof(Account)] = Fk(),
                [nameof(Category)] = Fk(),
                [nameof(Payee)] = Fk(),
                [nameof(Participant)] = Fk(),
                [nameof(SavingsGoal)] = Fk(),
                [nameof(Budget)] = Fk(("CategoryId", nameof(Category))),
                [nameof(Debt)] = Fk(("PayeeId", nameof(Payee)), ("AccountId", nameof(Account))),
                [nameof(PartyEvent)] = Fk(("MeParticipantId", nameof(Participant)), ("OwnTransactionId", nameof(Transaction))),
                [nameof(PartyMember)] = Fk(("PartyId", nameof(PartyEvent)), ("ParticipantId", nameof(Participant))),
                [nameof(PartyExpense)] = Fk(("PartyId", nameof(PartyEvent)), ("PaidByParticipantId", nameof(Participant))),
                [nameof(PartyExpenseExclusion)] = Fk(("ExpenseId", nameof(PartyExpense)), ("ParticipantId", nameof(Participant))),
                [nameof(PartyContribution)] = Fk(("PartyId", nameof(PartyEvent)), ("ParticipantId", nameof(Participant))),
                [nameof(PartyPayment)] = Fk(("PartyId", nameof(PartyEvent)), ("FromId", nameof(Participant)), ("ToId", nameof(Participant))),
                [nameof(Pocket)] = Fk(("AccountId", nameof(Account))),
                [nameof(PocketMovement)] = Fk(("PocketId", nameof(Pocket)), ("TransactionId", nameof(Transaction)), ("TransferId", nameof(Transfer))),
                [nameof(QuickEntry)] = Fk(("CategoryId", nameof(Category)), ("AccountId", nameof(Account)), ("PayeeId", nameof(Payee))),
                [nameof(RecurringTransaction)] = Fk(("CategoryId", nameof(Category)), ("AccountId", nameof(Account)), ("PayeeId", nameof(Payee)), ("DebtId", nameof(Debt))),
                [nameof(SavingsChallenge)] = Fk(("ToAccountId", nameof(Account)), ("ToPocketId", nameof(Pocket))),
                [nameof(SavingsChallengeDay)] = Fk(("ChallengeId", nameof(SavingsChallenge)), ("TransferId", nameof(Transfer)),
                    ("SourceMovementId", nameof(PocketMovement)), ("DestinationMovementId", nameof(PocketMovement))),
                [nameof(SavingsGoalAccount)] = Fk(("GoalId", nameof(SavingsGoal)), ("AccountId", nameof(Account))),
                [nameof(SavingsGoalPocket)] = Fk(("GoalId", nameof(SavingsGoal)), ("PocketId", nameof(Pocket))),
                [nameof(Transfer)] = Fk(("SourceAccountId", nameof(Account)), ("DestinationAccountId", nameof(Account))),
                [nameof(Trip)] = Fk(("AccountId", nameof(Account))),
                [nameof(TripCategory)] = Fk(("LastCategoryId", nameof(Category))),
                [nameof(TripBudget)] = Fk(("TripId", nameof(Trip)), ("TripCategoryId", nameof(TripCategory))),
                [nameof(Transaction)] = Fk(("CategoryId", nameof(Category)), ("AccountId", nameof(Account)), ("PayeeId", nameof(Payee)),
                    ("DebtId", nameof(Debt)), ("TripId", nameof(Trip)), ("TripCategoryId", nameof(TripCategory)))
            };

        /// <summary>The synced tables, every table after the ones it points at.</summary>
        public static readonly IReadOnlyList<string> Order = TopologicalOrder();

        public static Type TypeOf(string table) =>
            SyncCapture.SyncedTables.First(t => SyncCapture.TableName(t) == table);

        /// <summary>The (table, column) pairs that point at <paramref name="parent"/>.</summary>
        public static IEnumerable<(string Table, string Column)> ReferencesTo(string parent) =>
            ForeignKeys.SelectMany(t => t.Value.Where(c => c.Value == parent).Select(c => (t.Key, c.Key)));

        private static IReadOnlyList<string> TopologicalOrder()
        {
            var ordered = new List<string>();
            var visiting = new HashSet<string>();
            void Visit(string table)
            {
                if (ordered.Contains(table))
                    return;
                if (!visiting.Add(table))
                    throw new InvalidOperationException($"Foreign keys of {table} form a cycle.");
                foreach (var parent in ForeignKeys[table].Values.Distinct())
                    Visit(parent);
                visiting.Remove(table);
                ordered.Add(table);
            }
            foreach (var table in ForeignKeys.Keys.Order(StringComparer.Ordinal))
                Visit(table);
            return ordered;
        }
    }
}
