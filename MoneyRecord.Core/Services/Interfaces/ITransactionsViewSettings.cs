using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>How the Transactions list is grouped: remembered between visits (by date until the user picks another).</summary>
    public interface ITransactionsViewSettings
    {
        GroupingMode Grouping { get; set; }
    }
}
