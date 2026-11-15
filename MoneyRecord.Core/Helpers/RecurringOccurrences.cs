using System.Globalization;
using MoneyRecord.Models;
using SQLite;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// The key that names one occurrence of a schedule the same on every device (Stage 26.4b). Local IDs differ between paired
    /// devices, so the key uses the schedule's sync id; with sync off the schedule has none and there is nothing to match.
    /// </summary>
    public static class RecurringOccurrences
    {
        public static string? KeyFor(SQLiteConnection db, int recurringId, DateTime occurrence)
        {
            var gid = db.ExecuteScalar<string?>("SELECT GlobalId FROM SyncMeta WHERE TableName = ? AND LocalId = ?", nameof(RecurringTransaction), recurringId);
            return gid == null ? null : string.Create(CultureInfo.InvariantCulture, $"{gid}:{occurrence:yyyyMMdd}");
        }
    }
}
