namespace MoneyRecord.Helpers
{
    /// <summary>The description is optional on entry: an empty one is saved as the category's name.</summary>
    public static class EntryDescription
    {
        public static string Resolve(string? description, string categoryName) =>
            string.IsNullOrWhiteSpace(description) ? categoryName : description.Trim();
    }
}
