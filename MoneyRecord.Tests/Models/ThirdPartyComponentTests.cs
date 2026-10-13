using System.Xml.Linq;
using AwesomeAssertions;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Models;

/// <summary>The About page's licence list stays in step with the packages and the bundled licence files.</summary>
public class ThirdPartyComponentTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static string LicensesFolder =>
        Path.Combine(RepoRoot, "MoneyRecord", "Resources", "Raw", ThirdPartyComponent.LicensesFolder);

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MoneyRecord.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    [Theory]
    [InlineData("MoneyRecord/MoneyRecord.csproj")]
    [InlineData("MoneyRecord.Core/MoneyRecord.Core.csproj")]
    public void Every_shipped_package_is_listed(string project)
    {
        var packages = XDocument.Load(Path.Combine(RepoRoot, project))
            .Descendants("PackageReference")
            .Select(p => (string)p.Attribute("Include")!);

        var listed = ThirdPartyComponent.All.SelectMany(c => c.Packages);

        packages.Should().BeSubsetOf(listed, "a new dependency needs its licence on the About page");
    }

    [Fact]
    public void Every_licence_file_exists_and_is_used()
    {
        var referenced = ThirdPartyComponent.All
            .Where(c => c.LicenseFile != null)
            .Select(c => c.LicenseFile!)
            .Distinct();
        var bundled = Directory.GetFiles(LicensesFolder).Select(Path.GetFileName);

        bundled.Should().BeEquivalentTo(referenced);
    }
}
