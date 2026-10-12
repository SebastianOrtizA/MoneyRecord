using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;
using MoneyRecord.Helpers;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class DebouncerTests
{
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(300);

    private readonly FakeTimeProvider _clock = new();
    private readonly Debouncer _sut;

    public DebouncerTests()
    {
        _sut = new Debouncer(Delay, _clock);
    }

    [Fact]
    public async Task Runs_only_the_last_of_several_quick_calls()
    {
        var runs = new List<int>();

        var first = _sut.RunAsync(() => runs.Add(1));
        _clock.Advance(TimeSpan.FromMilliseconds(100));
        var second = _sut.RunAsync(() => runs.Add(2));
        _clock.Advance(TimeSpan.FromMilliseconds(100));
        var third = _sut.RunAsync(() => runs.Add(3));
        _clock.Advance(Delay);
        await Task.WhenAll(first, second, third);

        runs.Should().Equal(3);
    }

    [Fact]
    public async Task Waits_for_the_whole_delay()
    {
        var ran = false;

        var pending = _sut.RunAsync(() => ran = true);
        _clock.Advance(Delay - TimeSpan.FromMilliseconds(1));
        ran.Should().BeFalse();

        _clock.Advance(TimeSpan.FromMilliseconds(1));
        await pending;
        ran.Should().BeTrue();
    }

    [Fact]
    public async Task Runs_again_after_a_quiet_period()
    {
        var runs = 0;

        var first = _sut.RunAsync(() => runs++);
        _clock.Advance(Delay);
        await first;
        var second = _sut.RunAsync(() => runs++);
        _clock.Advance(Delay);
        await second;

        runs.Should().Be(2);
    }

    [Fact]
    public async Task Cancel_drops_the_pending_action()
    {
        var ran = false;

        var pending = _sut.RunAsync(() => ran = true);
        _sut.Cancel();
        _clock.Advance(Delay);
        await pending;

        ran.Should().BeFalse();
    }
}
