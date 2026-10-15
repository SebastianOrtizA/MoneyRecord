using System.Xml.Linq;
using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Services;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>
/// Android backup copies only what the rule files include. Their three lists (Android 12+ cloud backup and device
/// transfer, Android 11 and older full backup) must match, and must name the files the app really writes.
/// </summary>
public class AndroidBackupRulesTests
{
    private static string RulesFile(string name) =>
        TestRepo.PathOf("MoneyRecord", "Platforms", "Android", "Resources", "xml", name);

    private static IEnumerable<string> Includes(XElement section) =>
        section.Elements("include").Select(e => $"{(string)e.Attribute("domain")!}:{(string)e.Attribute("path")!}");

    public static TheoryData<string> Sections => new()
    {
        "cloud-backup",
        "device-transfer",
        "full-backup-content",
    };

    private static XElement Section(string name)
    {
        if (name == "full-backup-content")
            return XDocument.Load(RulesFile("backup_rules.xml")).Root!;
        return XDocument.Load(RulesFile("data_extraction_rules.xml")).Root!.Element(name)!;
    }

    [Theory]
    [MemberData(nameof(Sections))]
    public void Every_rule_list_backs_up_the_database_receipts_and_preferences(string section)
    {
        Includes(Section(section)).Should().BeEquivalentTo(
        [
            $"file:{AppPaths.DatabaseFileName}",
            $"file:{ReceiptFiles.FolderName}/",
            "sharedpref:.",
        ]);
    }

    /// <summary>
    /// SecureStorage (the database key) is encrypted with a Keystore key that stays on the phone: a restored copy
    /// can't be read. Its file is named after the application ID.
    /// </summary>
    [Theory]
    [MemberData(nameof(Sections))]
    public void Every_rule_list_leaves_out_secure_storage(string section)
    {
        var applicationId = XDocument.Load(TestRepo.PathOf("MoneyRecord", "MoneyRecord.csproj"))
            .Descendants("ApplicationId").Single().Value;

        Section(section).Elements("exclude")
            .Select(e => $"{(string)e.Attribute("domain")!}:{(string)e.Attribute("path")!}")
            .Should().Equal($"sharedpref:{applicationId}.microsoft.maui.essentials.preferences.xml");
    }
}
