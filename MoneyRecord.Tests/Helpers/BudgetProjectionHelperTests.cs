using FluentAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class BudgetProjectionHelperTests
{
    [Theory]
    [InlineData(PeriodType.Today, 100, 100)]
    [InlineData(PeriodType.LastWeek, 100, 700)]
    [InlineData(PeriodType.LastMonth, 100, 3000)]
    [InlineData(PeriodType.LastYear, 100, 36500)]
    public void DayBudget_StaticPeriods_CalculatesCorrectly(PeriodType periodType, decimal limit, decimal expected)
    {
        var result = BudgetProjectionHelper.CalculateProjectedLimit(
            limit, BudgetPeriod.Day, periodType);

        result.Should().Be(expected);
    }

    [Fact]
    public void DayBudget_CalendarMonth_MultipliesByDaysInCurrentMonth()
    {
        var result = BudgetProjectionHelper.CalculateProjectedLimit(
            10m, BudgetPeriod.Day, PeriodType.CalendarMonth);

        var expectedDays = DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month);
        result.Should().Be(10m * expectedDays);
    }

    [Fact]
    public void DayBudget_CalendarYear_MultipliesByDaysInCurrentYear()
    {
        var result = BudgetProjectionHelper.CalculateProjectedLimit(
            10m, BudgetPeriod.Day, PeriodType.CalendarYear);

        var expectedDays = DateTime.IsLeapYear(DateTime.Now.Year) ? 366 : 365;
        result.Should().Be(10m * expectedDays);
    }

    [Fact]
    public void DayBudget_CustomPeriod_MultipliesByDaysInRange()
    {
        var start = new DateTime(2026, 3, 1);
        var end = new DateTime(2026, 3, 10);

        var result = BudgetProjectionHelper.CalculateProjectedLimit(
            20m, BudgetPeriod.Day, PeriodType.CustomPeriod, start, end);

        // 10 days inclusive
        result.Should().Be(200m);
    }

    [Theory]
    [InlineData(PeriodType.CalendarMonth, 600, 600)]
    [InlineData(PeriodType.CalendarYear, 600, 7200)]
    [InlineData(PeriodType.LastMonth, 600, 600)]
    [InlineData(PeriodType.LastYear, 600, 7200)]
    public void MonthBudget_StaticPeriods_CalculatesCorrectly(PeriodType periodType, decimal limit, decimal expected)
    {
        var result = BudgetProjectionHelper.CalculateProjectedLimit(
            limit, BudgetPeriod.Month, periodType);

        result.Should().Be(expected);
    }

    [Fact]
    public void MonthBudget_Today_DividesMonthlyLimitByDaysInMonth()
    {
        var result = BudgetProjectionHelper.CalculateProjectedLimit(
            300m, BudgetPeriod.Month, PeriodType.Today);

        var daysInMonth = DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month);
        result.Should().Be(300m / daysInMonth);
    }

    [Fact]
    public void MonthBudget_LastWeek_ProRatesFor7Days()
    {
        var result = BudgetProjectionHelper.CalculateProjectedLimit(
            300m, BudgetPeriod.Month, PeriodType.LastWeek);

        result.Should().Be(300m / 30 * 7);
    }

    [Fact]
    public void MonthBudget_CustomPeriod_ProRatesByDays()
    {
        var start = new DateTime(2026, 6, 1);
        var end = new DateTime(2026, 6, 15);

        var result = BudgetProjectionHelper.CalculateProjectedLimit(
            600m, BudgetPeriod.Month, PeriodType.CustomPeriod, start, end);

        // 15 days inclusive
        result.Should().Be(600m / 30 * 15);
    }

    [Theory]
    [InlineData(PeriodType.CalendarYear, 12000, 12000)]
    [InlineData(PeriodType.CalendarMonth, 12000, 1000)]
    [InlineData(PeriodType.LastMonth, 12000, 1000)]
    [InlineData(PeriodType.LastYear, 12000, 12000)]
    public void YearBudget_StaticPeriods_CalculatesCorrectly(PeriodType periodType, decimal limit, decimal expected)
    {
        var result = BudgetProjectionHelper.CalculateProjectedLimit(
            limit, BudgetPeriod.Year, periodType);

        result.Should().Be(expected);
    }

    [Fact]
    public void YearBudget_Today_DividesByDaysInYear()
    {
        var result = BudgetProjectionHelper.CalculateProjectedLimit(
            3650m, BudgetPeriod.Year, PeriodType.Today);

        var daysInYear = DateTime.IsLeapYear(DateTime.Now.Year) ? 366 : 365;
        result.Should().Be(3650m / daysInYear);
    }

    [Fact]
    public void YearBudget_LastWeek_ProRatesFor7Days()
    {
        var result = BudgetProjectionHelper.CalculateProjectedLimit(
            3650m, BudgetPeriod.Year, PeriodType.LastWeek);

        result.Should().Be(3650m / 365 * 7);
    }

    [Fact]
    public void YearBudget_CustomPeriod_ProRatesByDays()
    {
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 3, 31);

        var result = BudgetProjectionHelper.CalculateProjectedLimit(
            3650m, BudgetPeriod.Year, PeriodType.CustomPeriod, start, end);

        // 90 days inclusive
        result.Should().Be(3650m / 365 * 90);
    }

    [Fact]
    public void CustomPeriod_DefaultDates_ReturnsSingleDay()
    {
        var result = BudgetProjectionHelper.CalculateProjectedLimit(
            100m, BudgetPeriod.Day, PeriodType.CustomPeriod, default, default);

        // GetCustomPeriodDays returns 1 for default dates
        result.Should().Be(100m);
    }

    [Fact]
    public void UnknownBudgetPeriod_ReturnsOriginalLimit()
    {
        var result = BudgetProjectionHelper.CalculateProjectedLimit(
            500m, (BudgetPeriod)99, PeriodType.CalendarMonth);

        result.Should().Be(500m);
    }
}
