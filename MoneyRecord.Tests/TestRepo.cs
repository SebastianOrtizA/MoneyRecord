namespace MoneyRecord.Tests;

/// <summary>Files of the repository itself, for tests that check the app project (csproj, resources) stays consistent.</summary>
internal static class TestRepo
{
    public static readonly string Root = FindRoot();

    /// <summary>The absolute path of a file given relative to the repository root.</summary>
    public static string PathOf(params string[] parts) => Path.Combine([Root, .. parts]);

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MoneyRecord.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
