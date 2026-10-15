namespace MoneyRecord.Services.Interfaces
{
    /// <summary>Reminders as system notifications: bills before they are due, budgets near or over their limit.</summary>
    public interface IReminderService
    {
        /// <summary>
        /// The daily background job: reminds of the bills coming up, creates the recurring transactions that are due
        /// (also when reminders are off) and checks the budgets.
        /// </summary>
        Task RunDailyAsync();

        /// <summary>
        /// Notifies once per budget period when a budget reaches 80% of its limit, and once when it goes over. Best-effort
        /// (called after a save): a failure is logged, never thrown.
        /// </summary>
        Task CheckBudgetsAsync();

        /// <summary>
        /// Once, after the user saves a recurring transaction or a budget: offers to turn reminders on (and asks for the
        /// system permission). Best-effort like <see cref="CheckBudgetsAsync"/>.
        /// </summary>
        Task OfferAsync();

        /// <summary>
        /// Turns reminders on when the system allows notifications (asking first if needed). When they are blocked,
        /// offers to open the system settings and leaves reminders off. True when they are on.
        /// </summary>
        Task<bool> EnableAsync();
    }
}
