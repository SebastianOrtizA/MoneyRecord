using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class ReminderSettings(IPreferencesService preferences) : IReminderSettings
    {
        public const int BillsOff = -1;

        /// <summary>The choices Settings offers for <see cref="BillDaysBefore"/>.</summary>
        public static readonly IReadOnlyList<int> BillDayChoices = [BillsOff, 0, 1, 2, 3, 7];

        private const string EnabledKey = "Reminders.Enabled";
        private const string BillDaysKey = "Reminders.BillDaysBefore";
        private const string BudgetAlertsKey = "Reminders.BudgetAlerts";
        private const string RecurringAddedKey = "Reminders.RecurringAdded";
        private const string OfferedKey = "Reminders.Offered";

        public bool Enabled
        {
            get => preferences.Get(EnabledKey, false);
            set => preferences.Set(EnabledKey, value);
        }

        public int BillDaysBefore
        {
            get => preferences.Get(BillDaysKey, 1);
            set => preferences.Set(BillDaysKey, value);
        }

        public bool BudgetAlerts
        {
            get => preferences.Get(BudgetAlertsKey, true);
            set => preferences.Set(BudgetAlertsKey, value);
        }

        public bool RecurringAdded
        {
            get => preferences.Get(RecurringAddedKey, true);
            set => preferences.Set(RecurringAddedKey, value);
        }

        public bool Offered
        {
            get => preferences.Get(OfferedKey, false);
            set => preferences.Set(OfferedKey, value);
        }
    }
}
