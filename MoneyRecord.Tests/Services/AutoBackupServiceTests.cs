using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>
/// AutoBackupService with the clock at 16 October 2026 09:00, automatic backups on, a reachable in-memory folder and a
/// substitute export that writes a small file and reports the data's fingerprint (<see cref="_fingerprint"/>).
/// </summary>
public sealed class AutoBackupServiceTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 16, 9, 0, 0);

    private readonly string _appData = Path.Combine(Path.GetTempPath(), $"moneyrecord-autobackup-{Guid.NewGuid():N}");
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(Now, TimeSpan.Zero));
    private readonly IDataExportService _export = Substitute.For<IDataExportService>();
    private readonly FakeBackupFolder _folder = new();
    private readonly AutoBackupSettings _settings = new(new FakePreferences()) { Enabled = true };
    private readonly FakeBackupPasswordStore _password = new();
    private readonly AutoBackupService _sut;
    private string _fingerprint = "data-1";

    public AutoBackupServiceTests()
    {
        _clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        _export.GetFingerprintAsync().Returns(_ => _fingerprint);
        _export.ExportBackupAsync(Arg.Any<string>(), Arg.Any<string?>()).Returns(call =>
        {
            var path = call.ArgAt<string>(0);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, _fingerprint);
            return _fingerprint;
        });
        _sut = new AutoBackupService(_export, _folder, _settings, _password, new AppPaths(_appData), _clock,
            Substitute.For<ILogger<AutoBackupService>>());
    }

    public void Dispose()
    {
        if (Directory.Exists(_appData))
            Directory.Delete(_appData, recursive: true);
    }

    [Theory]
    [InlineData(BackupFrequency.Daily, "2026-10-15 09:30", true)]   // a little later yesterday: still due today
    [InlineData(BackupFrequency.Daily, "2026-10-16 08:00", false)]
    [InlineData(BackupFrequency.Weekly, "2026-10-09 23:00", true)]
    [InlineData(BackupFrequency.Weekly, "2026-10-10 00:00", false)]
    [InlineData(BackupFrequency.Monthly, "2026-09-16 10:00", true)]
    [InlineData(BackupFrequency.Monthly, "2026-09-17 00:00", false)]
    public void IsDue_CountsWholeDaysFromTheLastBackup(BackupFrequency frequency, string last, bool due) =>
        AutoBackupService.IsDue(DateTime.Parse(last, System.Globalization.CultureInfo.InvariantCulture), frequency, Now)
            .Should().Be(due);

    [Fact]
    public void IsDue_WithoutABackupYet() =>
        AutoBackupService.IsDue(null, BackupFrequency.Monthly, Now).Should().BeTrue();

    [Fact]
    public async Task RunIfDue_WritesATimestampedBackupAndRemembersIt()
    {
        await _sut.RunIfDueAsync();

        _folder.Files.Keys.Should().Equal("MoneyRecord_AutoBackup_20261016_090000.zip");
        _folder.Files.Values.Single().Should().Be("data-1");
        _settings.LastBackup.Should().Be(Now);
        _settings.LastFingerprint.Should().Be("data-1");
        Directory.GetFiles(Path.Combine(_appData, AppPaths.BackupsFolderName)).Should().BeEmpty("the temp copy is deleted");
    }

    [Fact]
    public async Task RunIfDue_UsesTheBackupPassword()
    {
        _password.Password = "s3cret";

        await _sut.RunIfDueAsync();

        await _export.Received(1).ExportBackupAsync(Arg.Any<string>(), "s3cret");
    }

    [Fact]
    public async Task RunIfDue_NotDue_DoesNotReadTheData()
    {
        _settings.LastBackup = Now.AddDays(-2);

        await _sut.RunIfDueAsync();

        _folder.Files.Should().BeEmpty();
        await _export.DidNotReceive().GetFingerprintAsync();
    }

    [Fact]
    public async Task RunIfDue_Unchanged_SkipsWhileTheLastBackupIsInTheFolder()
    {
        await _sut.RunIfDueAsync();
        _clock.Advance(TimeSpan.FromDays(7));

        await _sut.RunIfDueAsync();

        _folder.Files.Should().HaveCount(1);
        _settings.LastBackup.Should().Be(Now, "the last backup still holds this data");
    }

    [Fact]
    public async Task RunIfDue_Unchanged_ButNoBackupInTheFolder_WritesOne()
    {
        await _sut.RunIfDueAsync();
        _folder.Files.Clear();   // deleted by the user, or a new folder was picked
        _clock.Advance(TimeSpan.FromDays(7));

        await _sut.RunIfDueAsync();

        _folder.Files.Keys.Should().Equal("MoneyRecord_AutoBackup_20261023_090000.zip");
    }

    [Fact]
    public async Task Backups_KeepTheNewestOnly_AndNeverTouchOtherFiles()
    {
        _settings.Keep = 3;
        _settings.Frequency = BackupFrequency.Daily;
        _folder.Files["MoneyRecord_Backup_20200101_000000.zip"] = "manual";
        _folder.Files["notes.txt"] = "mine";

        for (var day = 0; day < 5; day++)
        {
            _fingerprint = $"data-{day}";
            await _sut.RunIfDueAsync();
            _clock.Advance(TimeSpan.FromDays(1));
        }

        _folder.Files.Keys.Should().BeEquivalentTo(
            "MoneyRecord_AutoBackup_20261018_090000.zip",
            "MoneyRecord_AutoBackup_20261019_090000.zip",
            "MoneyRecord_AutoBackup_20261020_090000.zip",
            "MoneyRecord_Backup_20200101_000000.zip",
            "notes.txt");
    }

    [Fact]
    public async Task RunIfDue_Off_OrFolderUnreachable_DoesNothing()
    {
        _settings.Enabled = false;
        await _sut.RunIfDueAsync();
        _settings.Enabled = true;
        _folder.Name = null;
        await _sut.RunIfDueAsync();

        _folder.Files.Should().BeEmpty();
        _settings.LastFailure.Should().BeNull("Settings shows the unreachable folder instead");
    }

    [Fact]
    public async Task RunIfDue_Failure_IsRememberedNotThrown()
    {
        _folder.FailWrites = true;

        await _sut.Invoking(s => s.RunIfDueAsync()).Should().NotThrowAsync();

        _settings.LastFailure.Should().Be(Now);
        _settings.LastBackup.Should().BeNull();
        _settings.LastFingerprint.Should().BeNull();
    }

    [Fact]
    public async Task BackUpNow_WritesEvenWhenUnchanged_AndClearsAFailure()
    {
        await _sut.RunIfDueAsync();
        _settings.LastFailure = Now;
        _clock.Advance(TimeSpan.FromMinutes(1));

        await _sut.BackUpNowAsync();

        _folder.Files.Should().HaveCount(2);
        _settings.LastFailure.Should().BeNull();
    }

    [Fact]
    public async Task BackUpNow_Failure_IsRememberedAndThrown()
    {
        _export.ExportBackupAsync(Arg.Any<string>(), Arg.Any<string?>()).ThrowsAsync(new IOException("disk full"));

        await _sut.Invoking(s => s.BackUpNowAsync()).Should().ThrowAsync<IOException>();

        _settings.LastFailure.Should().Be(Now);
    }
}

/// <summary>A backup folder in memory: file name → content.</summary>
internal sealed class FakeBackupFolder : IBackupFolder
{
    public Dictionary<string, string> Files { get; } = [];

    public bool FailWrites { get; set; }

    public bool Picks { get; set; } = true;

    public bool IsSupported => true;

    public string? Name { get; set; } = "Drive";

    public Task<bool> PickAsync()
    {
        if (Picks)
            Name = "Picked";
        return Task.FromResult(Picks);
    }

    public IReadOnlyList<string> ListFiles() => Files.Keys.ToList();

    public void Write(string fileName, string sourcePath)
    {
        if (FailWrites)
            throw new IOException("The provider refused the file");
        Files[fileName] = File.ReadAllText(sourcePath);
    }

    public void Delete(string fileName) => Files.Remove(fileName);
}

/// <summary>The backup password in memory.</summary>
internal sealed class FakeBackupPasswordStore : IBackupPasswordStore
{
    public bool IsSupported => true;

    public string? Password { get; set; }

    public Task<string?> GetAsync() => Task.FromResult(Password);

    public Task SetAsync(string? password)
    {
        Password = string.IsNullOrEmpty(password) ? null : password;
        return Task.CompletedTask;
    }
}
