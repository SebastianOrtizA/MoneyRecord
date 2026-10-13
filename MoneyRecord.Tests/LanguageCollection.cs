using MoneyRecord.Services;
using Xunit;

namespace MoneyRecord.Tests;

/// <summary>
/// Tests that switch the app language change global state (CurrentCulture, AppResources.Culture), so they run alone,
/// after the parallel tests.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LanguageCollection
{
    public const string Name = "Language";
}

/// <summary>Starts a test in English and gives the device language back afterwards.</summary>
public abstract class LanguageTestBase : IDisposable
{
    protected LanguageTestBase() => LocalizationService.Instance.SetLanguage("en", new FakePreferences());

    public void Dispose()
    {
        LocalizationService.Instance.SetLanguage(string.Empty, new FakePreferences());
        GC.SuppressFinalize(this);
    }
}
