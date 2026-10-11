namespace MoneyRecord.Services.Interfaces
{
    public interface IErrorHandler
    {
        Task HandleAsync(Exception ex, string userMessage);
        Task HandleAsync(string userMessage);
    }
}
