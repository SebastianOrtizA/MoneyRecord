namespace MoneyRecord.Services.Interfaces
{
    public interface IRecurringTransactionService
    {
        Task<int> ProcessDueTransactionsAsync();
    }
}
