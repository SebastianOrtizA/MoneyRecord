namespace MoneyRecord.Models
{
    /// <summary>
    /// Third-party software shipped in the app, for the About page (AGPL distribution with MIT / Apache-2.0 parts must
    /// pass their notices on). <see cref="LicenseFile"/> is the full text in the app's <c>Licenses/</c> assets.
    /// <para>
    /// Scope: every direct package reference (checked by a test), plus what ships with them in the Android app: the
    /// .NET runtime and .NET for Android with their notices, the AndroidX / Google / Kotlin libraries and Glide pulled
    /// in by MAUI, and SkiaSharp's native libraries. Those transitive ones are not checked by the test: after a MAUI or
    /// SkiaSharp update, look at the APK's dependencies (<c>obj/project.assets.json</c>) for new licences.
    /// </para>
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
            new(".NET Runtime and Microsoft.Extensions", "MIT + bundled notices", ".NET Foundation and Contributors",
                "https://github.com/dotnet/runtime", "dotnet-runtime.txt",
                ["Microsoft.Extensions.Logging.Abstractions", "Microsoft.Extensions.Logging.Debug"]),
            new(".NET for Android", "MIT + bundled notices", ".NET Foundation and Contributors",
                "https://github.com/dotnet/android", "dotnet-android.txt", []),
            new("Android libraries (AndroidX, Material Components, Kotlin, Guava, Tink, Gson, …)", "Apache-2.0",
                "The Android Open Source Project; Google LLC; JetBrains s.r.o. and other contributors",
                "https://developer.android.com/jetpack/androidx", "Apache-2.0.txt",
                ["Xamarin.AndroidX.Work.Runtime", "Xamarin.AndroidX.Biometric"]),
            new("Glide", "BSD-2-Clause, MIT and Apache-2.0 parts", "Google, Inc.",
                "https://github.com/bumptech/glide", "Glide.txt", []),
            new(".NET Community Toolkit (MVVM)", "MIT", ".NET Foundation and Contributors",
                "https://github.com/CommunityToolkit/dotnet", "CommunityToolkit-dotnet.txt", ["CommunityToolkit.Mvvm"]),
            new(".NET MAUI Community Toolkit", "MIT", ".NET Foundation and Contributors",
                "https://github.com/CommunityToolkit/Maui", "CommunityToolkit-Maui.txt", ["CommunityToolkit.Maui"]),
            new("sqlite-net", "MIT", "Krueger Systems, Inc.",
                "https://github.com/praeclarum/sqlite-net", "sqlite-net.txt", ["sqlite-net-pcl"]),
            new("SQLitePCL.raw", "Apache-2.0", "Eric Sink",
                "https://github.com/ericsink/SQLitePCL.raw", "Apache-2.0.txt",
                ["SQLitePCLRaw.core", "SQLitePCLRaw.provider.e_sqlite3"]),
            new("SQLite3 Multiple Ciphers", "MIT", "Ulrich Telle",
                "https://github.com/utelle/SQLite3MultipleCiphers", "SQLite3MultipleCiphers.txt", ["SQLite3MC.PCLRaw.bundle"]),
            new("Serilog", "Apache-2.0", "Serilog Contributors",
                "https://github.com/serilog/serilog", "Apache-2.0.txt",
                ["Serilog.Extensions.Logging", "Serilog.Sinks.File"]),
            new("Sentry SDKs (.NET, Android, Native)", "MIT", "Sentry and individual contributors",
                "https://github.com/getsentry/sentry-dotnet", "Sentry.txt", ["Sentry.Maui"]),
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
