namespace MoneyRecord.Models
{
    /// <summary>The system notification channel a reminder goes to (each can be muted in the system settings).</summary>
    public enum ReminderChannel
    {
        /// <summary>Bills before they are due, recurring transactions added.</summary>
        Bills,
        Budgets
    }

    /// <summary>
    /// A system notification. One with the same <paramref name="Id"/> replaces the previous one; a tap opens the app on
    /// <paramref name="Route"/>.
    /// </summary>
    public sealed record AppNotification(int Id, ReminderChannel Channel, string Title, string Message, string Route);
}
