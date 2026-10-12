using AwesomeAssertions;
using MoneyRecord.Helpers;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class DebouncerTests
{
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(50);

    [Fact]
    public async Task Runs_only_the_last_of_several_quick_calls()
    {
        var debouncer = new Debouncer(Delay);
        var runs = new List<int>();

        var first = debouncer.RunAsync(() => runs.Add(1));
        var second = debouncer.RunAsync(() => runs.Add(2));
        var third = debouncer.RunAsync(() => runs.Add(3));
        await Task.WhenAll(first, second, third);

        runs.Should().Equal(3);
    }

    [Fact]
    public async Task Runs_again_after_a_quiet_period()
    {
        var debouncer = new Debouncer(Delay);
        var runs = 0;

        await debouncer.RunAsync(() => runs++);
        await debouncer.RunAsync(() => runs++);

        runs.Should().Be(2);
    }

    [Fact]
    public async Task Cancel_drops_the_pending_action()
    {
        var debouncer = new Debouncer(Delay);
        var ran = false;

        var pending = debouncer.RunAsync(() => ran = true);
        debouncer.Cancel();
        await pending;

        ran.Should().BeFalse();
    }
}
