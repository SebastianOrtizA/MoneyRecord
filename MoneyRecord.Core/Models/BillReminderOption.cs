using MoneyRecord.Helpers;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;

namespace MoneyRecord.Models
{
    /// <summary>A Settings choice for bill reminders: off, on the day, or some days before, named in the current language.</summary>
    public sealed record BillReminderOption(int DaysBefore, string Name)
    {
        public static IReadOnlyList<BillReminderOption> All() =>
            ReminderSettings.BillDayChoices.Select(days => new BillReminderOption(days, days switch
            {
                ReminderSettings.BillsOff => AppResources.BillRemindersOff,
                0 => AppResources.BillRemindersSameDay,
                _ => Plurals.DaysBefore(days)
            })).ToList();
    }
}
