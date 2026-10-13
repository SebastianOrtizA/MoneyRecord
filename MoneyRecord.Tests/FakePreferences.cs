using MoneyRecord.Services;

namespace MoneyRecord.Tests;

/// <summary>Preferences kept in memory.</summary>
internal sealed class FakePreferences : IPreferencesService
{
    private readonly Dictionary<string, object?> _values = [];

    public T Get<T>(string key, T defaultValue) =>
        _values.TryGetValue(key, out var value) && value is T typed ? typed : defaultValue;

    public void Set<T>(string key, T value) => _values[key] = value;
}
