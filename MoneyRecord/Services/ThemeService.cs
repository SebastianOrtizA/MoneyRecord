using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary><see cref="IThemeService"/> on <see cref="Application.UserAppTheme"/>.</summary>
    public sealed class ThemeService : IThemeService
    {
        public const string ThemePreferenceKey = "Theme";

        private readonly IPreferencesService _preferences;

        public ThemeService(IPreferencesService preferences)
        {
            _preferences = preferences;
        }

        public ThemeChoice Current =>
            Enum.TryParse<ThemeChoice>(_preferences.Get(ThemePreferenceKey, string.Empty), out var choice)
                ? choice
                : ThemeChoice.System;

        public void ApplySaved() => Apply(Current);

        public void Set(ThemeChoice choice)
        {
            _preferences.Set(ThemePreferenceKey, choice.ToString());
            Apply(choice);
        }

        private static void Apply(ThemeChoice choice)
        {
            if (Application.Current == null)
                return;

            Application.Current.UserAppTheme = choice switch
            {
                ThemeChoice.Light => AppTheme.Light,
                ThemeChoice.Dark => AppTheme.Dark,
                _ => AppTheme.Unspecified
            };
        }
    }
}
