using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;

namespace MoneyRecord.Services
{
    public sealed class RecurringTransactionService : IRecurringTransactionService
    {
        private readonly IRecurringTransactionRepository _recurringRepo;
        private readonly DatabaseInitializer _database;
        private readonly TimeProvider _clock;

        public RecurringTransactionService(IRecurringTransactionRepository recurringRepo, DatabaseInitializer database,
            TimeProvider clock)
        {
            _recurringRepo = recurringRepo;
            _database = database;
            _clock = clock;
        }

        /// <summary>
        /// Creates every occurrence that is due (catching up the periods missed while the app was closed) and moves
        /// each schedule on. A recurrence whose next occurrence is past its end date is deactivated.
        /// </summary>
        public async Task<int> ProcessDueTransactionsAsync()
        {
            var now = _clock.GetLocalNow().DateTime;
            var created = 0;

            foreach (var recurring in await _recurringRepo.GetDueAsync(now))
            {
                // One database transaction per recurrence: its new transactions and its advanced schedule are saved
                // together, so an interrupted catch-up never creates the same occurrence twice
                var count = 0;
                await _database.RunInTransactionAsync(db =>
                {
                    while (recurring.NextOccurrence <= now && !HasEnded(recurring))
                    {
                        db.Insert(new Transaction
                        {
                            Date = recurring.NextOccurrence,
                            Description = recurring.Description,
                            Amount = recurring.Amount,
                            CategoryId = recurring.CategoryId,
                            Type = recurring.TransactionType,
                            AccountId = recurring.AccountId
                        });
                        count++;

                        recurring.LastProcessedDate = recurring.NextOccurrence;
                        recurring.NextOccurrence = RecurrenceCalculator.Next(
                            recurring.NextOccurrence, recurring.Frequency, recurring.EffectiveDayOfMonth);
                    }

                    if (HasEnded(recurring))
                        recurring.IsActive = false;
                    db.Update(recurring);
                });
                created += count;
            }

            return created;
        }

        private static bool HasEnded(RecurringTransaction recurring) =>
            recurring.EndDate.HasValue && recurring.NextOccurrence > recurring.EndDate.Value;
    }
}
