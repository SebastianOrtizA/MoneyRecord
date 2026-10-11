using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class RecurringTransactionService : IRecurringTransactionService
    {
        private readonly IRecurringTransactionRepository _recurringRepo;
        private readonly ITransactionRepository _transactionRepo;

        public RecurringTransactionService(
            IRecurringTransactionRepository recurringRepo,
            ITransactionRepository transactionRepo)
        {
            _recurringRepo = recurringRepo;
            _transactionRepo = transactionRepo;
        }

        public async Task<int> ProcessDueTransactionsAsync()
        {
            var now = DateTime.Now;
            var dueItems = await _recurringRepo.GetDueAsync(now);
            int created = 0;

            foreach (var recurring in dueItems)
            {
                if (recurring.EndDate.HasValue && recurring.NextOccurrence > recurring.EndDate.Value)
                {
                    await _recurringRepo.DeactivateAsync(recurring.Id);
                    continue;
                }

                while (recurring.NextOccurrence <= now)
                {
                    if (recurring.EndDate.HasValue && recurring.NextOccurrence > recurring.EndDate.Value)
                    {
                        await _recurringRepo.DeactivateAsync(recurring.Id);
                        break;
                    }

                    var transaction = new Transaction
                    {
                        Date = recurring.NextOccurrence,
                        Description = recurring.Description,
                        Amount = recurring.Amount,
                        CategoryId = recurring.CategoryId,
                        Type = recurring.TransactionType,
                        AccountId = recurring.AccountId
                    };

                    await _transactionRepo.SaveAsync(transaction);
                    created++;

                    recurring.LastProcessedDate = recurring.NextOccurrence;
                    recurring.NextOccurrence = CalculateNextOccurrence(recurring.NextOccurrence, recurring.Frequency, recurring.DayOfMonth);
                }

                await _recurringRepo.SaveAsync(recurring);
            }

            return created;
        }

        private static DateTime CalculateNextOccurrence(DateTime current, RecurrenceFrequency frequency, int? dayOfMonth)
        {
            if (frequency == RecurrenceFrequency.Monthly && dayOfMonth.HasValue)
            {
                var next = current.AddMonths(1);
                var day = Math.Min(dayOfMonth.Value, DateTime.DaysInMonth(next.Year, next.Month));
                return new DateTime(next.Year, next.Month, day, current.Hour, current.Minute, current.Second);
            }

            return frequency switch
            {
                RecurrenceFrequency.Daily => current.AddDays(1),
                RecurrenceFrequency.Weekly => current.AddDays(7),
                RecurrenceFrequency.Biweekly => current.AddDays(14),
                RecurrenceFrequency.Monthly => current.AddMonths(1),
                RecurrenceFrequency.Yearly => current.AddYears(1),
                _ => current.AddMonths(1)
            };
        }
    }
}
