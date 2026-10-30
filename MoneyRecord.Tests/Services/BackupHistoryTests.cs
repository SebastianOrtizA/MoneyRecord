using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;
using MoneyRecord.Services;
using Xunit;

namespace MoneyRecord.Tests.Services;

public class BackupHistoryTests
{
    [Fact]
    public void It_remembers_when_a_backup_was_last_written()
    {
        var preferences = new FakePreferences();
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 30, 9, 30, 0, TimeSpan.Zero));
        time.SetLocalTimeZone(TimeZoneInfo.Utc);
        var sut = new BackupHistory(preferences, time);

        sut.LastWritten.Should().BeNull();
        sut.MarkWritten();

        sut.LastWritten.Should().Be(new DateTime(2026, 10, 30, 9, 30, 0));
        new BackupHistory(preferences, time).LastWritten.Should().Be(new DateTime(2026, 10, 30, 9, 30, 0));
    }
}
