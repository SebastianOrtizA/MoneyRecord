using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;

namespace MoneyRecord.Models
{
    /// <summary>A Settings choice for the low-balance warning: off, or how many days ahead, named in the current language.</summary>
    public sealed record LowBalanceOption(int Days, string Name)
    {
        public static IReadOnlyList<LowBalanceOption> All() =>
            ReminderSettings.LowBalanceChoices.Select(days => new LowBalanceOption(days, days == ReminderSettings.LowBalanceOff
                ? AppResources.BillRemindersOff
                : string.Format(AppResources.LowBalanceWithinDays, days))).ToList();
    }
}
