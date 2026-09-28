using FluentAssertions;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Models;

public class BudgetProgressTests
{
    [Fact]
    public void CalculateProgress_UnderBudget_SetsCorrectValues()
    {
        var progress = new BudgetProgress
        {
            LimitAmount = 1000m,
            SpentAmount = 400m
        };

        progress.CalculateProgress();

        progress.ProgressPercentage.Should().Be(40);
        progress.IsOverBudget.Should().BeFalse();
        progress.RemainingAmount.Should().Be(600m);
        progress.ExceededAmount.Should().Be(0m);
        progress.ProgressColor.Should().Be(Colors.Green);
    }

    [Fact]
    public void CalculateProgress_OverBudget_SetsOverBudgetValues()
    {
        var progress = new BudgetProgress
        {
            LimitAmount = 500m,
            SpentAmount = 750m
        };

        progress.CalculateProgress();

        progress.ProgressPercentage.Should().Be(100);
        progress.IsOverBudget.Should().BeTrue();
        progress.ExceededAmount.Should().Be(250m);
        progress.RemainingAmount.Should().Be(0m);
        progress.ProgressColor.Should().Be(Colors.Red);
    }

    [Fact]
    public void CalculateProgress_ExactlyAtLimit_SetsRedColor()
    {
        var progress = new BudgetProgress
        {
            LimitAmount = 200m,
            SpentAmount = 200m
        };

        progress.CalculateProgress();

        progress.ProgressPercentage.Should().Be(100);
        progress.IsOverBudget.Should().BeFalse();
        progress.RemainingAmount.Should().Be(0m);
        progress.ProgressColor.Should().Be(Colors.Red);
    }

    [Fact]
    public void CalculateProgress_At80Percent_SetsOrangeColor()
    {
        var progress = new BudgetProgress
        {
            LimitAmount = 100m,
            SpentAmount = 85m
        };

        progress.CalculateProgress();

        progress.ProgressPercentage.Should().Be(85);
        progress.ProgressColor.Should().Be(Colors.Orange);
    }

    [Fact]
    public void CalculateProgress_At60Percent_SetsYellowColor()
    {
        var progress = new BudgetProgress
        {
            LimitAmount = 100m,
            SpentAmount = 65m
        };

        progress.CalculateProgress();

        progress.ProgressPercentage.Should().Be(65);
        progress.ProgressColor.Should().Be(Colors.Yellow);
    }

    [Fact]
    public void CalculateProgress_ZeroLimit_SetsZeroProgress()
    {
        var progress = new BudgetProgress
        {
            LimitAmount = 0m,
            SpentAmount = 100m
        };

        progress.CalculateProgress();

        progress.ProgressPercentage.Should().Be(0);
        progress.IsOverBudget.Should().BeTrue();
        progress.ExceededAmount.Should().Be(100m);
    }

    [Fact]
    public void CalculateProgress_NoSpending_SetsGreenColor()
    {
        var progress = new BudgetProgress
        {
            LimitAmount = 500m,
            SpentAmount = 0m
        };

        progress.CalculateProgress();

        progress.ProgressPercentage.Should().Be(0);
        progress.IsOverBudget.Should().BeFalse();
        progress.RemainingAmount.Should().Be(500m);
        progress.ProgressColor.Should().Be(Colors.Green);
    }
}
