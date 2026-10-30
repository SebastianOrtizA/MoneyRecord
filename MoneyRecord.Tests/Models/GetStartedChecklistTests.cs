using AwesomeAssertions;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Models;

public class GetStartedChecklistTests
{
    private static GetStartedState State(bool hasTransactions = false, bool backedUp = false, bool canLock = true, bool lockOn = false,
        bool remindersOn = false) => new(hasTransactions, backedUp, canLock, lockOn, remindersOn);

    [Fact]
    public void Nothing_done_lists_every_step_open_in_order()
    {
        var checklist = GetStartedChecklist.Build(State());

        checklist.Rows.Select(r => r.Step).Should().Equal(GetStartedStep.AddEntry, GetStartedStep.Backup, GetStartedStep.AppLock,
            GetStartedStep.Reminders);
        checklist.Rows.Should().OnlyContain(r => !r.Done);
        checklist.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void Each_row_is_done_by_its_own_state()
    {
        GetStartedChecklist.Build(State(hasTransactions: true)).Rows.Single(r => r.Done).Step.Should().Be(GetStartedStep.AddEntry);
        GetStartedChecklist.Build(State(backedUp: true)).Rows.Single(r => r.Done).Step.Should().Be(GetStartedStep.Backup);
        GetStartedChecklist.Build(State(lockOn: true)).Rows.Single(r => r.Done).Step.Should().Be(GetStartedStep.AppLock);
        GetStartedChecklist.Build(State(remindersOn: true)).Rows.Single(r => r.Done).Step.Should().Be(GetStartedStep.Reminders);
    }

    [Fact]
    public void Without_a_phone_lock_the_app_lock_row_is_left_out()
    {
        var checklist = GetStartedChecklist.Build(State(canLock: false, lockOn: true));

        checklist.Rows.Select(r => r.Step).Should().Equal(GetStartedStep.AddEntry, GetStartedStep.Backup, GetStartedStep.Reminders);
    }

    [Fact]
    public void It_is_complete_when_every_row_it_shows_is_done()
    {
        GetStartedChecklist.Build(State(true, true, canLock: false, remindersOn: true)).IsComplete.Should().BeTrue();
        GetStartedChecklist.Build(State(true, true, canLock: true, remindersOn: true)).IsComplete.Should().BeFalse();
        GetStartedChecklist.Build(State(true, true, true, true, true)).IsComplete.Should().BeTrue();
    }
}
