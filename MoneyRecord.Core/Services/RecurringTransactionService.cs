using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;
using SQLite;

namespace MoneyRecord.Services
{
    public sealed class RecurringTransactionService : IRecurringTransactionService
    {
        private readonly IRecurringTransactionRepository _recurringRepo;
        private readonly ICategoryRepository _categoryRepo;
        private readonly IAccountRepository _accountRepo;
        private readonly DatabaseInitializer _database;
        private readonly TimeProvider _clock;
        private readonly ILogger<RecurringTransactionService> _logger;

        // App start and the daily background job can run at the same time: one run at a time, each reading the
        // schedules the previous run advanced, so no occurrence is created twice
        private readonly SemaphoreSlim _processing = new(1, 1);

        public RecurringTransactionService(IRecurringTransactionRepository recurringRepo,
            ICategoryRepository categoryRepo, IAccountRepository accountRepo, DatabaseInitializer database,
            TimeProvider clock, ILogger<RecurringTransactionService>? logger = null)
        {
            _logger = logger ?? NullLogger<RecurringTransactionService>.Instance;
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
            // What is left of each debt caps its scheduled payments
            var debtsLeft = recurrings.Any(r => r.DebtId != null)
                ? await DebtLedger.GetOutstandingAsync(await _database.GetConnectionAsync())
                : null;
            return UpcomingSchedule.Build(recurrings, categories, accounts, _clock.GetLocalNow().DateTime, days, debtsLeft);
        }

        public async Task<List<UpcomingEntry>> GetPendingAsync() =>
            (await GetUpcomingAsync(0)).Where(e => e.IsPending).ToList();

        public Task EnterOccurrenceAsync(int recurringId, Transaction transaction) =>
            MoveOnAsync(recurringId, transaction);

        public Task SkipOccurrenceAsync(int recurringId) => MoveOnAsync(recurringId, null);

        /// <summary>
        /// The next occurrence of a variable-amount schedule is entered (<paramref name="entry"/> saved, its amount kept as the
        /// estimate) or skipped (null); either way the schedule moves on in the same database transaction.
        /// </summary>
        private async Task MoveOnAsync(int recurringId, Transaction? entry)
        {
            await _processing.WaitAsync();
            try
            {
                await _database.RunInTransactionAsync(db =>
                {
                    var recurring = db.Find<RecurringTransaction>(recurringId);
                    if (recurring is not { IsActive: true, IsVariableAmount: true })
                        throw new InvalidOperationException($"Recurring transaction {recurringId} has nothing to enter");
                    if (HasEnded(recurring) || recurring.NextOccurrence > _clock.GetLocalNow().DateTime)
                        throw new InvalidOperationException($"Recurring transaction {recurringId} has no occurrence due");

                    if (entry != null)
                    {
                        if (entry.Date.Date != recurring.NextOccurrence.Date)
                            throw new InvalidOperationException($"{entry.Date:d} is not the next occurrence of recurring transaction {recurringId}");
                        db.Insert(entry);
                        recurring.LastAmountCents = Math.Abs(entry.AmountCents);
                    }

                    Advance(recurring);
                    if (HasEnded(recurring))
                        recurring.IsActive = false;
                    db.Update(recurring);
                });
            }
            finally
            {
                _processing.Release();
            }
        }

        /// <summary>
        /// Creates every occurrence that is due (catching up the periods missed while the app was closed) and moves
        /// each schedule on. A recurrence whose next occurrence is past its end date is deactivated. Runs one at a time.
        /// </summary>
        public async Task<RecurringRun> ProcessDueTransactionsAsync()
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

        private async Task<RecurringRun> ProcessDueAsync()
        {
            var now = _clock.GetLocalNow().DateTime;
            var created = 0;
            var paidOff = new List<int>();
            var paysDebts = false;

            foreach (var recurring in await _recurringRepo.GetDueAsync(now))
            {
                // Counted only once its database transaction is saved
                var createdHere = 0;
                int? paidOffHere = null;
                try
                {
                    // One database transaction per recurrence: its new transactions and its advanced schedule are saved
                    // together, so an interrupted catch-up never creates the same occurrence twice (a rollback rethrows)
                    await _database.RunInTransactionAsync(db =>
                    {
                        while (recurring.NextOccurrence <= now && !HasEnded(recurring))
                        {
                            if (recurring.DebtId is { } debtId)
                            {
                                paysDebts = true;
                                if (!PayDebt(db, recurring, debtId))
                                    break;
                                createdHere++;
                                Advance(recurring);
                                // That payment settled the debt
                                if (!recurring.IsActive)
                                {
                                    paidOffHere = debtId;
                                    break;
                                }
                                continue;
                            }

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
                            createdHere++;
                            Advance(recurring);
                        }

                        if (HasEnded(recurring))
                            recurring.IsActive = false;
                        db.Update(recurring);
                    });
                }
                catch (Exception ex)
                {
                    // One recurrence that can't be processed must not stop the others; it is tried again next time
                    _logger.LogError(ex, "Recurring transaction {RecurringId} could not be processed", recurring.Id);
                    continue;
                }

                created += createdHere;
                if (paidOffHere is { } paid)
                    paidOff.Add(paid);
            }

            // A payment's interest may have created the "Interest" category
            if (paysDebts)
                _categoryRepo.InvalidateCache();
            return new RecurringRun(created, paidOff);
        }

        private static void Advance(RecurringTransaction recurring)
        {
            recurring.LastProcessedDate = recurring.NextOccurrence;
            recurring.NextOccurrence = RecurrenceCalculator.Next(
                recurring.NextOccurrence, recurring.Frequency, recurring.EffectiveDayOfMonth);
        }

        /// <summary>
        /// One scheduled debt payment, capped at what is left. False when there is nothing to pay (the debt is paid or
        /// gone) or its account can't pay it any more (moved to another currency): the schedule stops. A payment that
        /// settles the debt stops it as well (<see cref="RecurringTransaction.IsActive"/>).
        /// </summary>
        private bool PayDebt(SQLiteConnection db, RecurringTransaction recurring, int debtId)
        {
            var debt = db.Find<Debt>(debtId);
            var left = debt == null ? 0 : DebtLedger.GetTotals(db, debtId).OutstandingCents;
            if (debt == null || left == 0)
            {
                recurring.IsActive = false;
                return false;
            }
            if (!DebtLedger.CanPayThrough(db, debt, recurring.AccountId))
            {
                _logger.LogWarning("Debt {DebtId}'s schedule stopped: account {AccountId} is gone or in another currency",
                    debtId, recurring.AccountId);
                recurring.IsActive = false;
                return false;
            }

            if (DebtLedger.RecordPayment(db, debt, recurring.AccountId, recurring.NextOccurrence,
                    Math.Min(recurring.AmountCents, left), recurring.InterestCents))
                recurring.IsActive = false;
            return true;
        }

        private static bool HasEnded(RecurringTransaction recurring) =>
            recurring.EndDate.HasValue && recurring.NextOccurrence > recurring.EndDate.Value;
    }
}
