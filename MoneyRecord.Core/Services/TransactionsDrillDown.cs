using MoneyRecord.Models;

namespace MoneyRecord.Services
{
    /// <summary>
    /// A report's request to open the Transactions tab filtered (singleton): the report sets it and navigates, and the
    /// Transactions tab takes it once on its next load. The period travels in <see cref="ViewModels.PeriodViewModel"/>.
    /// </summary>
    public sealed class TransactionsDrillDown
    {
        private (int? CategoryId, TransactionType? Type)? _pending;

        public void Request(int? categoryId, TransactionType? type) => _pending = (categoryId, type);

        /// <summary>The pending request, cleared; false when there is none.</summary>
        public bool TryTake(out int? categoryId, out TransactionType? type)
        {
            (categoryId, type) = _pending ?? (null, null);
            var had = _pending != null;
            _pending = null;
            return had;
        }
    }
}
