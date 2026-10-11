using AwesomeAssertions;
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
        progress.Level.Should().Be(BudgetLevel.OnTrack);
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
        progress.Level.Should().Be(BudgetLevel.Exceeded);
    }

    [Fact]
    public void CalculateProgress_ExactlyAtLimit_IsExceeded()
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
        progress.Level.Should().Be(BudgetLevel.Exceeded);
    }

    [Fact]
    public void CalculateProgress_At80Percent_IsHigh()
    {
        var progress = new BudgetProgress
        {
            LimitAmount = 100m,
            SpentAmount = 85m
        };

        progress.CalculateProgress();

        progress.ProgressPercentage.Should().Be(85);
        progress.Level.Should().Be(BudgetLevel.High);
    }

    [Fact]
    public void CalculateProgress_At60Percent_IsWarning()
    {
        var progress = new BudgetProgress
        {
            LimitAmount = 100m,
            SpentAmount = 65m
        };

        progress.CalculateProgress();

        progress.ProgressPercentage.Should().Be(65);
        progress.Level.Should().Be(BudgetLevel.Warning);
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
    public void CalculateProgress_NoSpending_IsOnTrack()
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
        progress.Level.Should().Be(BudgetLevel.OnTrack);
    }
}
