using MoneyRecord.Helpers;
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
                    recurring.NextOccurrence = RecurrenceCalculator.Next(recurring.NextOccurrence, recurring.Frequency, recurring.DayOfMonth);
                }

                await _recurringRepo.SaveAsync(recurring);
            }

            return created;
        }

    }
}
