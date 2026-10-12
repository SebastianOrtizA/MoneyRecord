namespace MoneyRecord.Models
{
    /// <summary>
    /// The Home list's search, filters and sort order. Null fields don't filter; category and account match by ID
    /// (a transfer matches both of its accounts).
    /// </summary>
    public sealed record TransactionFilter(
        string? SearchText = null,
        TransactionType? Type = null,
        int? CategoryId = null,
        int? AccountId = null,
        decimal? MinAmount = null,
        decimal? MaxAmount = null,
        bool Ascending = false)
    {
        /// <summary>True when anything other than the sort order narrows the list.</summary>
        public bool IsActive =>
            !string.IsNullOrWhiteSpace(SearchText) || Type != null || CategoryId != null || AccountId != null ||
            MinAmount != null || MaxAmount != null;
    }

    /// <summary>A category or account in a filter picker; <see cref="Name"/> is display only.</summary>
    public sealed record FilterOption(int Id, string Name);
}
