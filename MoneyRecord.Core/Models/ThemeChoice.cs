using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Models
{
    /// <summary>The app theme setting: follow the device, or always light / dark.</summary>
    public enum ThemeChoice
    {
        System,
        Light,
        Dark
    }

    /// <summary>A theme in a picker, named in the current language.</summary>
    public sealed record ThemeOption(ThemeChoice Choice, string Name)
    {
        public static IReadOnlyList<ThemeOption> All() =>
        [
            new(ThemeChoice.System, AppResources.SystemDefault),
            new(ThemeChoice.Light, AppResources.ThemeLight),
            new(ThemeChoice.Dark, AppResources.ThemeDark),
        ];
    }
}
