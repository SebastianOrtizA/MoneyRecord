namespace MoneyRecord.Services.Repositories
{
    /// <summary>The optional date bounds of a sum: dated on or after <c>since</c>, and before <c>before</c>.</summary>
    internal static class DateFilter
    {
        /// <returns>" AND …" conditions on the <c>Date</c> column (empty without bounds) and their arguments.</returns>
        public static (string Where, object[] Args) Sql(DateTime? since, DateTime? before) =>
            (since, before) switch
            {
                (null, null) => ("", []),
                ({ } from, null) => (" AND Date >= ?", [from]),
                (null, { } to) => (" AND Date < ?", [to]),
                ({ } from, { } to) => (" AND Date >= ? AND Date < ?", [from, to])
            };
    }
}
