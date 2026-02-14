namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// Base repository interface for generic CRUD operations.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    public interface IRepository<T> where T : class
    {
        Task<List<T>> GetAllAsync();
        Task<T?> GetByIdAsync(int id);
        Task<int> SaveAsync(T entity);
        Task<int> DeleteAsync(T entity);
    }
}
