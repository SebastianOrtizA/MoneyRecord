using System.Runtime.CompilerServices;
using MoneyRecord.Services;

namespace MoneyRecord.Tests;

/// <summary>
/// <see cref="LocalizationService.Instance"/> sets the app culture when it is first used. Whichever test touched it first
/// could switch the language between two reads (a text, then its expected text), so it is created once, in English,
/// before any test runs.
/// </summary>
internal static class TestAssemblyInit
{
    [ModuleInitializer]
    internal static void SetEnglish() => LocalizationService.Instance.SetLanguage("en", new FakePreferences());
}
