namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// Interface for database initialization and connection management.
    /// </summary>
    public interface IDatabaseInitializer
    {
        Task InitializeAsync();
        bool IsInitialized { get; }
    }
}
