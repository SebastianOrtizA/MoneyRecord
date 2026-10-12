using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using SQLite;

namespace MoneyRecord.Services.Repositories
{
    /// <summary>
    /// Create, read, update and delete for one table. Every call makes sure the database is initialized first.
    /// <see cref="OnChanged"/> runs after each write, for repositories that cache rows.
    /// </summary>
    public abstract class RepositoryBase<T> : IRepository<T> where T : class, IEntity, new()
    {
        private readonly DatabaseInitializer _database;

        protected RepositoryBase(DatabaseInitializer database)
        {
            _database = database;
        }

        protected Task<SQLiteAsyncConnection> ConnectionAsync() => _database.GetConnectionAsync();

        protected async Task<AsyncTableQuery<T>> TableAsync() => (await ConnectionAsync()).Table<T>();

        public virtual async Task<List<T>> GetAllAsync() => await (await TableAsync()).ToListAsync();

        public virtual async Task<T?> GetByIdAsync(int id) => await (await ConnectionAsync()).FindAsync<T>(id);

        /// <summary>Inserts a new row (ID 0, which then gets its ID) or updates an existing one.</summary>
        public async Task<int> SaveAsync(T entity)
        {
            var db = await ConnectionAsync();
            var result = entity.Id != 0 ? await db.UpdateAsync(entity) : await db.InsertAsync(entity);
            OnChanged();
            return result;
        }

        public async Task<int> DeleteAsync(T entity)
        {
            var result = await (await ConnectionAsync()).DeleteAsync(entity);
            OnChanged();
            return result;
        }

        /// <summary>Deletes the row with this ID; 0 when there is none.</summary>
        public async Task<int> DeleteByIdAsync(int id)
        {
            var result = await (await ConnectionAsync()).DeleteAsync<T>(id);
            OnChanged();
            return result;
        }

        protected virtual void OnChanged()
        {
        }
    }
}
