using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;

namespace MoneyRecord.Services
{
    public sealed class RecurringTransactionService : IRecurringTransactionService
    {
        private readonly IRecurringTransactionRepository _recurringRepo;
        private readonly ICategoryRepository _categoryRepo;
        private readonly IAccountRepository _accountRepo;
        private readonly DatabaseInitializer _database;
        private readonly TimeProvider _clock;

        // App start and the daily background job can run at the same time: one run at a time, each reading the
        // schedules the previous run advanced, so no occurrence is created twice
        private readonly SemaphoreSlim _processing = new(1, 1);

        public RecurringTransactionService(IRecurringTransactionRepository recurringRepo,
            ICategoryRepository categoryRepo, IAccountRepository accountRepo, DatabaseInitializer database,
            TimeProvider clock)
        {
            _recurringRepo = recurringRepo;
            _categoryRepo = categoryRepo;
            _accountRepo = accountRepo;
            _database = database;
            _clock = clock;
        }

        public async Task<List<UpcomingEntry>> GetUpcomingAsync(int days = UpcomingSchedule.DefaultDays)
        {
            var recurrings = await _recurringRepo.GetActiveAsync();
            if (recurrings.Count == 0)
                return [];

            var categories = (await _categoryRepo.GetAllAsync()).ToDictionary(c => c.Id);
            var accounts = (await _accountRepo.GetAllAsync()).ToDictionary(a => a.Id);
            return UpcomingSchedule.Build(recurrings, categories, accounts, _clock.GetLocalNow().DateTime, days);
        }

        /// <summary>
        /// Creates every occurrence that is due (catching up the periods missed while the app was closed) and moves
        /// each schedule on. A recurrence whose next occurrence is past its end date is deactivated. Runs one at a time.
        /// </summary>
        public async Task<int> ProcessDueTransactionsAsync()
        {
            await _processing.WaitAsync();
            try
            {
                return await ProcessDueAsync();
            }
            finally
            {
                _processing.Release();
            }
        }

        private async Task<int> ProcessDueAsync()
        {
            var now = _clock.GetLocalNow().DateTime;
            var created = 0;

            foreach (var recurring in await _recurringRepo.GetDueAsync(now))
            {
                // One database transaction per recurrence: its new transactions and its advanced schedule are saved
                // together, so an interrupted catch-up never creates the same occurrence twice (a rollback rethrows)
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
                            AccountId = recurring.AccountId,
                            PayeeId = recurring.PayeeId
                        });
                        created++;

                        recurring.LastProcessedDate = recurring.NextOccurrence;
                        recurring.NextOccurrence = RecurrenceCalculator.Next(
                            recurring.NextOccurrence, recurring.Frequency, recurring.EffectiveDayOfMonth);
                    }

                    if (HasEnded(recurring))
                        recurring.IsActive = false;
                    db.Update(recurring);
                });
            }

            return created;
        }

        private static bool HasEnded(RecurringTransaction recurring) =>
            recurring.EndDate.HasValue && recurring.NextOccurrence > recurring.EndDate.Value;
    }
}
