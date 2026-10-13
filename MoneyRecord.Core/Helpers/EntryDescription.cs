namespace MoneyRecord.Helpers
{
    /// <summary>
    /// The description is optional on entry: an empty one is saved as the category's name, and a saved one that equals
    /// the category's name counts as automatic again when editing, so it follows a category change.
    /// </summary>
    public static class EntryDescription
    {
        public static string Resolve(string? description, string categoryName) =>
            string.IsNullOrWhiteSpace(description) ? categoryName : description.Trim();

        /// <summary>What the edit form shows: empty for an automatic description (the placeholder shows the category).</summary>
        public static string ForEditing(string savedDescription, string? categoryName) =>
            savedDescription == categoryName ? string.Empty : savedDescription;
    }
}
