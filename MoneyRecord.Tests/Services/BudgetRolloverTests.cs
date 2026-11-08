using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using NSubstitute;
using Xunit;

namespace MoneyRecord.Tests.Services;

public class BudgetRolloverTests
{
    private static readonly DateTime Now = new(2026, 5, 15);

    [Fact]
    public void Unspent_money_carries_and_overspending_does_not()
    {
        // 100 limit: spend 60 (carry 40), spend 160 (limit 140 → overspent, carry 0), spend 0 (carry 100)
        BudgetRollover.Carry(100m, [60m, 160m, 0m]).Should().Be(100m);
        BudgetRollover.Carry(100m, [60m]).Should().Be(40m);
    }

    [Fact]
    public void Carry_accumulates_over_the_chain()
    {
        BudgetRollover.Carry(100m, [50m, 50m, 50m]).Should().Be(150m);
    }

    [Fact]
    public void No_earlier_period_means_no_carry()
    {
        BudgetRollover.Carry(100m, []).Should().Be(0m);
    }

    [Fact]
    public void The_chain_starts_in_the_month_the_budget_was_created()
    {
        var windows = BudgetRollover.PreviousWindows(BudgetPeriod.Month, Now, new DateTime(2026, 3, 20));

        windows.Select(w => w.Start).Should().Equal(new DateTime(2026, 3, 1), new DateTime(2026, 4, 1));
        windows[0].End.Should().Be(new DateTime(2026, 4, 1).AddTicks(-1));
    }

    [Fact]
    public void A_budget_created_this_month_has_nothing_to_carry()
    {
        BudgetRollover.PreviousWindows(BudgetPeriod.Month, Now, new DateTime(2026, 5, 2)).Should().BeEmpty();
    }

    [Fact]
    public void The_chain_is_capped_at_twelve_periods()
    {
        var windows = BudgetRollover.PreviousWindows(BudgetPeriod.Month, Now, new DateTime(2020, 1, 1));

        windows.Should().HaveCount(BudgetRollover.MaxPeriods);
        windows[0].Start.Should().Be(new DateTime(2025, 5, 1));
        windows[^1].Start.Should().Be(new DateTime(2026, 4, 1));
    }

    [Fact]
    public void Days_and_years_step_by_their_own_period()
    {
        BudgetRollover.PreviousWindows(BudgetPeriod.Day, Now, new DateTime(2026, 5, 13))
            .Select(w => w.Start).Should().Equal(new DateTime(2026, 5, 13), new DateTime(2026, 5, 14));
        BudgetRollover.PreviousWindows(BudgetPeriod.Year, Now, new DateTime(2024, 6, 1))
            .Select(w => w.Start).Should().Equal(new DateTime(2024, 1, 1), new DateTime(2025, 1, 1));
    }

    [Fact]
    public async Task A_budget_without_rollover_carries_nothing_and_asks_nothing()
    {
        var conversion = Substitute.For<IConversionService>();
        var budget = new Budget { CategoryId = 1, LimitAmount = 100m, CreatedDate = new DateTime(2026, 1, 1) };

        (await new BudgetRollover(conversion).CarryAsync(budget, Now)).Should().Be(0m);

        await conversion.DidNotReceiveWithAnyArgs().GetExpensesByCategoryAsync(default, default);
    }

    [Fact]
    public async Task Each_earlier_months_spending_is_read_once_for_all_budgets()
    {
        var conversion = Substitute.For<IConversionService>();
        var march = new DateTime(2026, 3, 1);
        var april = new DateTime(2026, 4, 1);
        conversion.GetExpensesByCategoryAsync(march, april.AddTicks(-1))
            .Returns(new Dictionary<int, BudgetSpent> { [1] = new(30m, []), [2] = new(150m, []) });
        conversion.GetExpensesByCategoryAsync(april, new DateTime(2026, 5, 1).AddTicks(-1))
            .Returns(new Dictionary<int, BudgetSpent> { [1] = new(100m, []) });
        var created = new DateTime(2026, 3, 10);
        var food = new Budget { CategoryId = 1, LimitAmount = 100m, RolloverUnspent = true, CreatedDate = created };
        var fun = new Budget { CategoryId = 2, LimitAmount = 100m, RolloverUnspent = true, CreatedDate = created };
        var sut = new BudgetRollover(conversion);

        // Food: March 100-30 = 70 carried; April limit 170, spent 100 → 70. Fun: March overspent → 0; April spent nothing → 100
        (await sut.CarryAsync(food, Now)).Should().Be(70m);
        (await sut.CarryAsync(fun, Now)).Should().Be(100m);

        await conversion.Received(2).GetExpensesByCategoryAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>());
    }
}
