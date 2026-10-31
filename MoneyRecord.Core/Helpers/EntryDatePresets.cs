namespace MoneyRecord.Helpers
{
    /// <summary>Which quick date chip of the add form a date matches: Today, Yesterday, or a picked date.</summary>
    public enum EntryDatePreset
    {
        Today,
        Yesterday,
        Other
    }

    /// <summary>The add form's date chips: today and yesterday from the clock, anything else is picked.</summary>
    public static class EntryDatePresets
    {
        public static DateTime Today(DateTime now) => now.Date;

        public static DateTime Yesterday(DateTime now) => now.Date.AddDays(-1);

        public static EntryDatePreset Classify(DateTime date, DateTime now) =>
            date.Date == now.Date ? EntryDatePreset.Today
            : date.Date == now.Date.AddDays(-1) ? EntryDatePreset.Yesterday
            : EntryDatePreset.Other;
    }
}
