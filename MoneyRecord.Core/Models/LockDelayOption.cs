using MoneyRecord.Helpers;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;

namespace MoneyRecord.Models
{
    /// <summary>A Settings choice for how long the app may stay in the background before it locks, in the current language.</summary>
    public sealed record LockDelayOption(int Minutes, string Name)
    {
        public static IReadOnlyList<LockDelayOption> All() =>
            PrivacySettings.LockDelays.Select(minutes => new LockDelayOption(minutes, minutes switch
            {
                // The one whole hour offered
                60 => AppResources.OneHour,
                _ => Plurals.Minutes(minutes)
            })).ToList();
    }
}
