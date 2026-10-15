namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// What the user wants to be reminded of (Settings → Reminders). Off until the user turns reminders on, from the
    /// one-time offer after saving a recurring transaction or a budget, or in Settings.
    /// </summary>
    public interface IReminderSettings
    {
        /// <summary>Reminders are on (the system permission was granted when they were turned on).</summary>
        bool Enabled { get; set; }

        /// <summary>
        /// Days before a recurring expense to remind of it: 0 = on the day, <see cref="ReminderSettings.BillsOff"/> = no
        /// bill reminders.
        /// </summary>
        int BillDaysBefore { get; set; }

        /// <summary>A budget reached 80% of its limit, or went over it.</summary>
        bool BudgetAlerts { get; set; }

        /// <summary>The daily job added recurring transactions.</summary>
        bool RecurringAdded { get; set; }

        /// <summary>The one-time offer was shown (or reminders were set in Settings), so it isn't shown again.</summary>
        bool Offered { get; set; }
    }
}
