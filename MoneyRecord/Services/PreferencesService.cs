namespace MoneyRecord.Services
{
    /// <summary>
    /// Default implementation of IPreferencesService using MAUI Preferences.
    /// Handles type conversion for supported preference types.
    /// </summary>
    public sealed class PreferencesService : IPreferencesService
    {
        public T Get<T>(string key, T defaultValue)
        {
            return defaultValue switch
            {
                string s => (T)(object)Preferences.Get(key, s),
                int i => (T)(object)Preferences.Get(key, i),
                bool b => (T)(object)Preferences.Get(key, b),
                double d => (T)(object)Preferences.Get(key, d),
                float f => (T)(object)Preferences.Get(key, f),
                long l => (T)(object)Preferences.Get(key, l),
                DateTime dt => (T)(object)Preferences.Get(key, dt),
                _ => defaultValue
            };
        }

        public void Set<T>(string key, T value)
        {
            switch (value)
            {
                case string s:
                    Preferences.Set(key, s);
                    break;
                case int i:
                    Preferences.Set(key, i);
                    break;
                case bool b:
                    Preferences.Set(key, b);
                    break;
                case double d:
                    Preferences.Set(key, d);
                    break;
                case float f:
                    Preferences.Set(key, f);
                    break;
                case long l:
                    Preferences.Set(key, l);
                    break;
                case DateTime dt:
                    Preferences.Set(key, dt);
                    break;
            }
        }
    }
}
