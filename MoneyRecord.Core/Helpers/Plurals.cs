using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using System.Globalization;

namespace MoneyRecord.Helpers
{
    /// <summary>Count + noun in the current language: "1 transaction", "0 transactions" / "1 transacción".</summary>
    public static class Plurals
    {
        /// <summary>The singular form: 1, and 0 too in a language that counts it so (French "0 transaction").</summary>
        private static bool IsOne(int count) =>
            count == 1 || (count == 0 && AppLanguage.For(CultureInfo.CurrentUICulture).ZeroIsSingular);

        public static string Transactions(int count) =>
            string.Format(IsOne(count) ? AppResources.TransactionCountOne : AppResources.TransactionCountOther, count);

        public static string Accounts(int count) =>
            string.Format(IsOne(count) ? AppResources.AccountCountOne : AppResources.AccountCountOther, count);

        public static string InDays(int count) =>
            string.Format(IsOne(count) ? AppResources.InDaysOne : AppResources.InDaysOther, count);

        public static string DaysBefore(int count) =>
            string.Format(IsOne(count) ? AppResources.DaysBeforeOne : AppResources.DaysBeforeOther, count);

        public static string RecurringAdded(int count) =>
            string.Format(IsOne(count) ? AppResources.RecurringAddedOne : AppResources.RecurringAddedOther, count);

        public static string Minutes(int count) =>
            string.Format(IsOne(count) ? AppResources.MinutesOne : AppResources.MinutesOther, count);

        public static string Backups(int count) =>
            string.Format(IsOne(count) ? AppResources.BackupsOne : AppResources.BackupsOther, count);

        public static string Recurring(int count) =>
            string.Format(IsOne(count) ? AppResources.RecurringCountOne : AppResources.RecurringCountOther, count);

        public static string Debts(int count) =>
            string.Format(IsOne(count) ? AppResources.DebtCountOne : AppResources.DebtCountOther, count);

        public static string Rows(int count) =>
            string.Format(IsOne(count) ? AppResources.ImportRowsOne : AppResources.ImportRowsOther, count);

        public static string ChallengeDaysLeft(int count) =>
            string.Format(IsOne(count) ? AppResources.ChallengeDaysLeftOne : AppResources.ChallengeDaysLeftOther, count);

        /// <summary>The import button: "Import 3 transactions".</summary>
        public static string ImportButton(int count) =>
            string.Format(IsOne(count) ? AppResources.ImportButtonOne : AppResources.ImportButtonOther, count);
    }
}
