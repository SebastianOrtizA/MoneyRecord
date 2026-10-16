using MoneyRecord.Models;

namespace MoneyRecord.Services.Repositories
{
    public sealed class PayeeRepository(DatabaseInitializer database)
        : RepositoryBase<Payee>(database), Interfaces.IPayeeRepository
    {
    }
}
