using System.Xml.Linq;
using AwesomeAssertions;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Models;

/// <summary>The About page's licence list stays in step with the packages and the bundled licence files.</summary>
public class ThirdPartyComponentTests
{
    private static string LicensesFolder =>
        TestRepo.PathOf("MoneyRecord", "Resources", "Raw", ThirdPartyComponent.LicensesFolder);

    [Theory]
    [InlineData("MoneyRecord/MoneyRecord.csproj")]
    [InlineData("MoneyRecord.Core/MoneyRecord.Core.csproj")]
    public void Every_shipped_package_is_listed(string project)
    {
        var packages = XDocument.Load(TestRepo.PathOf(project))
            .Descendants("PackageReference")
            .Select(p => (string)p.Attribute("Include")!);

        var listed = ThirdPartyComponent.All.SelectMany(c => c.Packages);

        packages.Should().BeSubsetOf(listed, "a new dependency needs its licence on the About page");
    }

    [Fact]
    public void THIRD_PARTY_NOTICES_lists_the_same_components_as_the_app()
    {
        var rows = File.ReadAllLines(TestRepo.PathOf("THIRD-PARTY-NOTICES.md"))
            .Where(line => line.StartsWith("| ") && !line.StartsWith("| Component |"))
            .Select(line => line.Split('|').Select(cell => cell.Trim()).ToArray())
            .ToList();

        rows.Select(r => r[1]).Should().Equal(ThirdPartyComponent.All.Select(c => c.Name),
            "the notices file follows ThirdPartyComponent.All, in the same order");
        foreach (var (row, component) in rows.Zip(ThirdPartyComponent.All))
        {
            row[2].Should().Be(component.License, component.Name);
            row[3].Should().Be(component.Copyright, component.Name);
            row[4].Should().Be(component.Url, component.Name);
            if (component.LicenseFile == null)
                row[5].Should().BeEmpty(component.Name);
            else
                row[5].Should().Contain($"[{component.LicenseFile}]", component.Name);
        }
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
