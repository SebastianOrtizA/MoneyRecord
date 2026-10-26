using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Helpers
{
    /// <summary>Count + noun in the current language: "1 transaction", "0 transactions" / "1 transacción".</summary>
    public static class Plurals
    {
        public static string Transactions(int count) =>
            string.Format(count == 1 ? AppResources.TransactionCountOne : AppResources.TransactionCountOther, count);

        public static string Accounts(int count) =>
            string.Format(count == 1 ? AppResources.AccountCountOne : AppResources.AccountCountOther, count);

        public static string InDays(int count) =>
            string.Format(count == 1 ? AppResources.InDaysOne : AppResources.InDaysOther, count);

        public static string DaysBefore(int count) =>
            string.Format(count == 1 ? AppResources.DaysBeforeOne : AppResources.DaysBeforeOther, count);

        public static string RecurringAdded(int count) =>
            string.Format(count == 1 ? AppResources.RecurringAddedOne : AppResources.RecurringAddedOther, count);

        public static string Minutes(int count) =>
            string.Format(count == 1 ? AppResources.MinutesOne : AppResources.MinutesOther, count);

        public static string Backups(int count) =>
            string.Format(count == 1 ? AppResources.BackupsOne : AppResources.BackupsOther, count);

        public static string Recurring(int count) =>
            string.Format(count == 1 ? AppResources.RecurringCountOne : AppResources.RecurringCountOther, count);

        public static string Debts(int count) =>
            string.Format(count == 1 ? AppResources.DebtCountOne : AppResources.DebtCountOther, count);

        public static string Rows(int count) =>
            string.Format(count == 1 ? AppResources.ImportRowsOne : AppResources.ImportRowsOther, count);

        public static string ChallengeDaysLeft(int count) =>
            string.Format(count == 1 ? AppResources.ChallengeDaysLeftOne : AppResources.ChallengeDaysLeftOther, count);

        /// <summary>The import button: "Import 3 transactions".</summary>
        public static string ImportButton(int count) =>
            string.Format(count == 1 ? AppResources.ImportButtonOne : AppResources.ImportButtonOther, count);
    }
}
