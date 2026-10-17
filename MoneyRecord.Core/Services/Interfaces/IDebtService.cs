using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// A new debt: lent to or borrowed from <paramref name="PersonName"/> (a payee, found or created). With an
    /// <paramref name="AccountId"/> the money moves through that account on <paramref name="Date"/>; without one it was owed
    /// already (nothing moves).
    /// </summary>
    public sealed record DebtDraft(DebtDirection Direction, string PersonName, string CurrencyCode, decimal Amount,
        int? AccountId, DateTime Date, string Note, DateTime? DueDate);

    /// <summary>Payments of <paramref name="Amount"/> (plus <paramref name="Interest"/>) every period from <paramref name="FirstDate"/>.</summary>
    public sealed record DebtScheduleDraft(decimal Amount, decimal Interest, RecurrenceFrequency Frequency, DateTime FirstDate, int AccountId);

    /// <summary>Debts and loans: the Debts page, payments, and the optional payment schedule.</summary>
    public interface IDebtService
    {
        /// <summary>Every debt with its amounts: open ones first (earliest due date first), paid ones last.</summary>
        Task<List<DebtSummary>> GetAllAsync();

        /// <summary>What is left of each debt, in cents (debt ID → cents), from one SQL query: caps scheduled payments.</summary>
        Task<Dictionary<int, long>> GetOutstandingAsync();

        /// <summary>The debt's history, newest first.</summary>
        Task<List<DebtMovement>> GetMovementsAsync(int debtId);

        /// <summary>Creates the debt, its hidden account, its start and its schedule in one database transaction.</summary>
        /// <returns>The new debt's ID.</returns>
        Task<int> AddAsync(DebtDraft draft, DebtScheduleDraft? schedule);

        /// <summary>Changes the person, note and due date, and sets (null = removes) the payment schedule.</summary>
        Task UpdateAsync(int debtId, string personName, string note, DateTime? dueDate, DebtScheduleDraft? schedule);

        /// <summary>Records a payment (at most what is left) and its interest; true when it paid the debt off.</summary>
        Task<bool> RecordPaymentAsync(int debtId, int accountId, DateTime date, decimal amount, decimal interest);

        /// <summary>More lent or borrowed through the account.</summary>
        Task AddToDebtAsync(int debtId, int accountId, DateTime date, decimal amount);

        /// <summary>Deletes one history line (its transfer or interest transaction).</summary>
        /// <exception cref="InvalidOperationException">
        /// Without it more would be paid back than was lent or borrowed (<see cref="DebtSummary.CanDelete"/> says so first).
        /// </exception>
        Task DeleteMovementAsync(DebtMovement movement);

        /// <summary>Deletes the debt with its transfers, interest, schedule and hidden account, in one database transaction.</summary>
        Task DeleteAsync(int debtId);
    }
}
