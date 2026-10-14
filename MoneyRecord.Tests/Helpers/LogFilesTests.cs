using System.IO.Compression;
using AwesomeAssertions;
using MoneyRecord.Helpers;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public sealed class LogFilesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"moneyrecord-logs-{Guid.NewGuid():N}");
    private readonly string _logs;
    private readonly string _zip;

    public LogFilesTests()
    {
        _logs = Path.Combine(_root, "logs");
        _zip = Path.Combine(_root, "logs.zip");
        Directory.CreateDirectory(_logs);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static Dictionary<string, string> Entries(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        return zip.Entries.ToDictionary(e => e.FullName, e =>
        {
            using var reader = new StreamReader(e.Open());
            return reader.ReadToEnd();
        });
    }

    [Fact]
    public void RollingPath_IsInTheLogsFolder()
    {
        LogFiles.RollingPath(_logs).Should().Be(Path.Combine(_logs, "moneyrecord-.log"));
    }

    [Fact]
    public void CreateArchive_ZipsEveryLogFile_OldestFirst()
    {
        // Past the size limit a day continues in _001, _002, ...
        File.WriteAllText(Path.Combine(_logs, "moneyrecord-20261014_001.log"), "today, later");
        File.WriteAllText(Path.Combine(_logs, "moneyrecord-20261014.log"), "today");
        File.WriteAllText(Path.Combine(_logs, "moneyrecord-20261013.log"), "yesterday");
        File.WriteAllText(Path.Combine(_logs, "notes.txt"), "not a log");

        LogFiles.CreateArchive(_logs, _zip).Should().BeTrue();

        Entries(_zip).Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["moneyrecord-20261013.log"] = "yesterday",
            ["moneyrecord-20261014.log"] = "today",
            ["moneyrecord-20261014_001.log"] = "today, later",
        });
        using var zip = ZipFile.OpenRead(_zip);
        zip.Entries.Select(e => e.FullName).Should().Equal(
            "moneyrecord-20261013.log", "moneyrecord-20261014.log", "moneyrecord-20261014_001.log");
    }

    [Fact]
    public void CreateArchive_ReadsTheFileTheLoggerHasOpen()
    {
        // Like Serilog's file sink: the writer keeps today's file open and only shares reading
        var today = Path.Combine(_logs, "moneyrecord-20261014.log");
        using (var writer = new StreamWriter(new FileStream(today, FileMode.Append, FileAccess.Write, FileShare.Read)))
        {
            writer.Write("still writing");
            writer.Flush();

            LogFiles.CreateArchive(_logs, _zip).Should().BeTrue();
        }

        Entries(_zip)["moneyrecord-20261014.log"].Should().Be("still writing");
    }

    [Fact]
    public void CreateArchive_WithoutLogs_WritesNothing()
    {
        LogFiles.CreateArchive(_logs, _zip).Should().BeFalse();
        LogFiles.CreateArchive(Path.Combine(_root, "missing"), _zip).Should().BeFalse();

        File.Exists(_zip).Should().BeFalse();
    }
}
