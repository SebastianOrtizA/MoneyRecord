using MoneyRecord.Models;

namespace MoneyRecord.Services
{
    /// <summary>Which quick entries the launcher's long-press menu offers.</summary>
    public static class QuickEntryShortcuts
    {
        /// <summary>The most a launcher menu gets.</summary>
        public const int MaxCount = 3;

        /// <summary>
        /// The most used quick entries with a fixed amount (one that asks for an amount needs the app anyway), the user's order
        /// breaking ties. Broken ones are left out.
        /// </summary>
        public static List<QuickEntryItem> Top(IEnumerable<QuickEntryItem> items, int count) =>
            items.Where(i => !i.AsksAmount && !i.IsBroken)
                .OrderByDescending(i => i.Entry.UseCount)
                .ThenBy(i => i.Entry.SortOrder)
                .ThenBy(i => i.Id)
                .Take(Math.Max(0, count))
                .ToList();
    }
}
