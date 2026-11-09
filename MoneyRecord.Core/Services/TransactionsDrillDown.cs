using MoneyRecord.Models;

namespace MoneyRecord.Services
{
    /// <summary>What a report asks the Transactions tab to show: a type, some categories (null/empty = all) a payee (named, so its chip shows even when no loaded entry has it) and a tag.</summary>
    public sealed record DrillDownTarget(TransactionType? Type, IReadOnlyList<int>? CategoryIds = null, FilterOption? Payee = null, string? Tag = null);

    /// <summary>
    /// A report's request to open the Transactions tab filtered (singleton): the report sets it and navigates, and the
    /// Transactions tab takes it once on its next load. The period travels in <see cref="ViewModels.PeriodViewModel"/>.
    /// </summary>
    public sealed class TransactionsDrillDown
    {
        private DrillDownTarget? _pending;

        public void Request(DrillDownTarget target) => _pending = target;

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
