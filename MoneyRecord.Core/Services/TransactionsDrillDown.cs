using MoneyRecord.Models;

namespace MoneyRecord.Services
{
    /// <summary>What a report asks the Transactions tab to show: a type and some categories (null/empty = all).</summary>
    public sealed record DrillDownTarget(TransactionType? Type, IReadOnlyList<int>? CategoryIds = null);

    /// <summary>
    /// A report's request to open the Transactions tab filtered (singleton): the report sets it and navigates, and the
    /// Transactions tab takes it once on its next load. The period travels in <see cref="ViewModels.PeriodViewModel"/>.
    /// </summary>
    public sealed class TransactionsDrillDown
    {
        private DrillDownTarget? _pending;

        public void Request(DrillDownTarget target) => _pending = target;

        public void Request(int? categoryId, TransactionType? type) =>
            Request(new DrillDownTarget(type, categoryId is int id ? [id] : null));

        /// <summary>The pending request, cleared; false when there is none.</summary>
        public bool TryTake(out DrillDownTarget target)
        {
            var had = _pending != null;
            target = _pending ?? new DrillDownTarget(null);
            _pending = null;
            return had;
        }
    }
}
