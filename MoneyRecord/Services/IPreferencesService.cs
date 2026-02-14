namespace MoneyRecord.Services
{
    /// <summary>
    /// Abstraction for accessing application preferences.
    /// Enables testability and follows interface segregation principle.
    /// </summary>
    public interface IPreferencesService
    {
        T Get<T>(string key, T defaultValue);
        void Set<T>(string key, T value);
    }
}
