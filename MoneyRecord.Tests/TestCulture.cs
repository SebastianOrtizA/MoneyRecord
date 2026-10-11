using System.Globalization;

namespace MoneyRecord.Tests;

internal static class TestCulture
{
    /// <summary>Runs <paramref name="action"/> with <paramref name="name"/> as the current culture, then restores it.</summary>
    public static void WithCulture(string name, Action action)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(name);
        try { action(); }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
