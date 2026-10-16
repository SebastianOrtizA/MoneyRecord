using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>The payees' rows (names for display); changes that touch transactions go through <see cref="IPayeeService"/>.</summary>
    public interface IPayeeRepository : IRepository<Payee>
    {
    }
}
