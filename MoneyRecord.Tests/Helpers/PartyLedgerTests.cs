using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public sealed class PartyLedgerTests
{
    private static readonly Participant Ana = new() { Id = 1, Name = "Ana" };
    private static readonly Participant Ben = new() { Id = 2, Name = "Ben" };
    private static readonly Participant Cam = new() { Id = 3, Name = "Cam" };
    private static readonly IReadOnlyDictionary<int, HashSet<int>> NoExclusions = new Dictionary<int, HashSet<int>>();

    private static PartyExpense Expense(int id, long cents, int? paidBy) =>
        new() { Id = id, AmountCents = cents, PaidByParticipantId = paidBy };

    [Theory]
    [InlineData(1000, 3)]
    [InlineData(1, 3)]
    [InlineData(100, 7)]
    [InlineData(0, 2)]
    public void ShareExpense_AddsUpExactly(long cents, int people)
    {
        var shares = PartyLedger.ShareExpense(Expense(5, cents, 1), Enumerable.Range(1, people));

        shares.Values.Sum().Should().Be(cents);
        (shares.Values.Max() - shares.Values.Min()).Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public void ShareExpense_LeftoverCentsRotateWithTheExpenseId()
    {
        var first = PartyLedger.ShareExpense(Expense(3, 100, 1), [1, 2, 3]);
        var second = PartyLedger.ShareExpense(Expense(4, 100, 1), [1, 2, 3]);

        first.Should().Equal(new Dictionary<int, long> { [1] = 34, [2] = 33, [3] = 33 });
        second.Should().Equal(new Dictionary<int, long> { [1] = 33, [2] = 34, [3] = 33 });
    }

    [Fact]
    public void ShareExpense_IsTheSameEveryTime()
    {
        var expense = Expense(7, 1001, 1);

        PartyLedger.ShareExpense(expense, [3, 1, 2]).Should().Equal(PartyLedger.ShareExpense(expense, [1, 2, 3]));
    }

    [Fact]
    public void ShareExpense_NobodyToShare_Throws()
    {
        var act = () => PartyLedger.ShareExpense(Expense(1, 100, 1), []);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Balances_PaidPlusHandedInMinusShare()
    {
        // 30.00 paid by Ana for all three, 12.00 paid by Ben only for Ben and Cam (Ana left out)
        var expenses = new[] { Expense(1, 3000, 1), Expense(2, 1200, 2) };
        var exclusions = new Dictionary<int, HashSet<int>> { [2] = [1] };

        var lines = PartyLedger.Balances([Ana, Ben, Cam], expenses, exclusions, []);

        lines.Single(l => l.ParticipantId == 1).BalanceCents.Should().Be(3000 - 1000);
        lines.Single(l => l.ParticipantId == 2).BalanceCents.Should().Be(1200 - 1000 - 600);
        lines.Single(l => l.ParticipantId == 3).BalanceCents.Should().Be(-1000 - 600);
        lines.Sum(l => l.BalanceCents).Should().Be(0);
    }

    [Fact]
    public void Balances_PotPaysAndCollects_StillAddUpToZero()
    {
        var contributions = new[]
        {
            new PartyContribution { ParticipantId = 1, AmountCents = 2000 },
            new PartyContribution { ParticipantId = 2, AmountCents = 1000 }
        };
        var expenses = new[] { Expense(1, 2400, null), Expense(2, 600, 3) };

        var lines = PartyLedger.Balances([Ana, Ben, Cam], expenses, NoExclusions, contributions);

        // 3000 handed in, 2400 spent from the pot: 600 left in it, so it owes 600 back
        lines.Single(l => l.ParticipantId == PartyLedger.PotId).BalanceCents.Should().Be(-600);
        lines.Sum(l => l.BalanceCents).Should().Be(0);
    }

    [Fact]
    public void SettleUp_LargestDebtorPaysLargestCreditor()
    {
        var lines = new[]
        {
            new PartyBalance(1, "Ana", 3000, 0, 1000),
            new PartyBalance(2, "Ben", 0, 0, 500),
            new PartyBalance(3, "Cam", 0, 0, 1500)
        };

        PartyLedger.SettleUp(lines).Should().Equal(new SettlePayment(3, 1, 1500), new SettlePayment(2, 1, 500));
    }

    [Fact]
    public void SettleUp_PaymentsBringEveryBalanceToZero()
    {
        var expenses = new[] { Expense(1, 1001, 1), Expense(2, 777, 2), Expense(3, 5000, null), Expense(4, 3, 3) };
        var contributions = new[] { new PartyContribution { ParticipantId = 3, AmountCents = 4000 } };
        var lines = PartyLedger.Balances([Ana, Ben, Cam], expenses, new Dictionary<int, HashSet<int>> { [2] = [1, 3] }, contributions);

        var payments = PartyLedger.SettleUp(lines);

        var left = lines.ToDictionary(l => l.ParticipantId, l => l.BalanceCents);
        foreach (var payment in payments)
        {
            payment.AmountCents.Should().BePositive();
            left[payment.FromId] += payment.AmountCents;
            left[payment.ToId] -= payment.AmountCents;
        }

        left.Values.Should().AllBeEquivalentTo(0);
        payments.Count.Should().BeLessThan(lines.Count);
    }

    [Fact]
    public void SettleUp_AlreadyEven_NoPayments() =>
        PartyLedger.SettleUp([new PartyBalance(1, "Ana", 500, 0, 500)]).Should().BeEmpty();

    [Fact]
    public void SettleUp_BalancesNotZero_Throws()
    {
        var act = () => PartyLedger.SettleUp([new PartyBalance(1, "Ana", 500, 0, 0)]);

        act.Should().Throw<ArgumentException>();
    }
}
