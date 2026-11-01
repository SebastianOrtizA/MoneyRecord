using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class GroupExpansionTests
{
    private static List<TransactionGroup> Groups(params string[] keys) =>
        keys.Select(k => new TransactionGroup(k, k, "F0770", TransactionType.Expense, [], [])).ToList();

    [Fact]
    public void Groups_start_closed()
    {
        var groups = Groups("a", "b");

        new GroupExpansion().Apply(groups, openByDefault: false);

        groups.Should().OnlyContain(g => !g.IsExpanded);
    }

    [Fact]
    public void An_opened_group_stays_open_when_the_groups_are_rebuilt()
    {
        var sut = new GroupExpansion();
        var first = Groups("a", "b");
        sut.Apply(first, false);
        sut.Toggle(first[1]);

        var rebuilt = Groups("a", "b");
        sut.Apply(rebuilt, false);

        rebuilt.Select(g => g.IsExpanded).Should().Equal(false, true);
    }

    [Fact]
    public void Groups_open_while_filtered_and_the_flips_are_forgotten_when_that_changes()
    {
        var sut = new GroupExpansion();
        var groups = Groups("a", "b");
        sut.Apply(groups, false);
        sut.Toggle(groups[0]);

        sut.Apply(groups, true);
        groups.Should().OnlyContain(g => g.IsExpanded);

        sut.Apply(groups, false);
        groups.Should().OnlyContain(g => !g.IsExpanded);
    }

    [Fact]
    public void Toggling_back_forgets_the_flip()
    {
        var sut = new GroupExpansion();
        var groups = Groups("a");
        sut.Apply(groups, false);
        sut.Toggle(groups[0]);
        sut.Toggle(groups[0]);

        var rebuilt = Groups("a");
        sut.Apply(rebuilt, false);

        rebuilt[0].IsExpanded.Should().BeFalse();
    }

    [Fact]
    public void Expand_all_is_remembered_and_reset_forgets_it()
    {
        var sut = new GroupExpansion();
        var groups = Groups("a", "b");
        sut.Apply(groups, false);
        sut.SetAll(groups, true);
        GroupExpansion.AnyCollapsed(groups).Should().BeFalse();

        var rebuilt = Groups("a", "b");
        sut.Apply(rebuilt, false);
        rebuilt.Should().OnlyContain(g => g.IsExpanded);

        sut.Reset();
        sut.Apply(rebuilt, false);
        rebuilt.Should().OnlyContain(g => !g.IsExpanded);
        GroupExpansion.AnyCollapsed(rebuilt).Should().BeTrue();
    }
}
