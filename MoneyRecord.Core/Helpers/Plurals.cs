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
    }
}
