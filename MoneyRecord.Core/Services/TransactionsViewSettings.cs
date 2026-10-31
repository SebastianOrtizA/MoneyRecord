using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class TransactionsViewSettings(IPreferencesService preferences) : ITransactionsViewSettings
    {
        private const string GroupingKey = "Transactions.Grouping";

        /// <summary>The saved mode; a missing or unknown value (an older or newer app) is <see cref="GroupingMode.Date"/>.</summary>
        public GroupingMode Grouping
        {
            get => Enum.TryParse<GroupingMode>(preferences.Get(GroupingKey, string.Empty), out var mode) && Enum.IsDefined(mode)
                ? mode
                : GroupingMode.Date;
            set => preferences.Set(GroupingKey, value.ToString());
        }
    }
}
