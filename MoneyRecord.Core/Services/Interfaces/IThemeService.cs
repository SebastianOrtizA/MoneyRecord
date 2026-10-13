using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>The saved app theme (System / Light / Dark).</summary>
    public interface IThemeService
    {
        ThemeChoice Current { get; }

        /// <summary>Applies the saved theme. Called once at startup.</summary>
        void ApplySaved();

        /// <summary>Saves and applies a theme.</summary>
        void Set(ThemeChoice choice);
    }
}
