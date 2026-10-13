namespace MoneyRecord.Models
{
    /// <summary>
    /// Third-party software shipped in the app, for the About page (AGPL distribution with MIT / Apache-2.0 parts must
    /// pass their notices on). <see cref="LicenseFile"/> is the full text in the app's <c>Licenses/</c> assets.
    /// </summary>
    /// <param name="Copyright">The holders, without "©"; empty for public-domain software.</param>
    /// <param name="Packages">The NuGet packages it covers (a test checks every package reference is listed).</param>
    public sealed record ThirdPartyComponent(
        string Name,
        string License,
        string Copyright,
        string Url,
        string? LicenseFile,
        IReadOnlyList<string> Packages)
    {
        public const string LicensesFolder = "Licenses";

        public static IReadOnlyList<ThirdPartyComponent> All { get; } =
        [
            new(".NET MAUI", "MIT", ".NET Foundation and Contributors",
                "https://github.com/dotnet/maui", "dotnet.txt", ["Microsoft.Maui.Controls"]),
            new(".NET Runtime (Microsoft.Extensions)", "MIT", ".NET Foundation and Contributors",
                "https://github.com/dotnet/runtime", "dotnet.txt",
                ["Microsoft.Extensions.Logging.Abstractions", "Microsoft.Extensions.Logging.Debug"]),
            new(".NET Community Toolkit (MVVM)", "MIT", ".NET Foundation and Contributors",
                "https://github.com/CommunityToolkit/dotnet", "CommunityToolkit-dotnet.txt", ["CommunityToolkit.Mvvm"]),
            new(".NET MAUI Community Toolkit", "MIT", ".NET Foundation and Contributors",
                "https://github.com/CommunityToolkit/Maui", "CommunityToolkit-Maui.txt", ["CommunityToolkit.Maui"]),
            new("sqlite-net", "MIT", "Krueger Systems, Inc.",
                "https://github.com/praeclarum/sqlite-net", "sqlite-net.txt", ["sqlite-net-pcl"]),
            new("SQLitePCL.raw", "Apache-2.0", "Eric Sink",
                "https://github.com/ericsink/SQLitePCL.raw", "Apache-2.0.txt", ["SQLitePCLRaw.bundle_e_sqlite3"]),
            new("SQLite", "Public domain", string.Empty,
                "https://sqlite.org/copyright.html", null, []),
            new("LiveCharts2", "MIT", "Alberto Rodriguez Orozco",
                "https://github.com/beto-rodriguez/LiveCharts2", "LiveCharts2.txt", ["LiveChartsCore.SkiaSharpView.Maui"]),
            new("SkiaSharp / HarfBuzzSharp (with Skia, HarfBuzz, FreeType, ICU, …)", "MIT + bundled notices",
                "Xamarin, Inc.; Microsoft Corporation; see the notices",
                "https://github.com/mono/SkiaSharp", "SkiaSharp.txt", []),
            new("Material Design Icons", "Apache-2.0 (font and icons)", "Pictogrammers",
                "https://pictogrammers.com/library/mdi/", "MaterialDesignIcons.txt", []),
            new("Open Sans", "Apache-2.0", "Google Corporation",
                "https://fonts.google.com/specimen/Open+Sans", "Apache-2.0.txt", []),
        ];
    }
}
