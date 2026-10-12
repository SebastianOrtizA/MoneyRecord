using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class DateRangeHelperTests
{
    [Fact]
    public void CalendarMonth_ReturnsFirstToLastDayOfCurrentMonth()
    {
        var (start, end) = DateRangeHelper.GetDateRange(PeriodType.CalendarMonth);

        var now = DateTime.Now;
        var lastDay = DateTime.DaysInMonth(now.Year, now.Month);
        start.Should().Be(new DateTime(now.Year, now.Month, 1));
        end.Day.Should().Be(lastDay);
        end.Should().BeAfter(new DateTime(now.Year, now.Month, lastDay));
    }

    [Fact]
    public void CalendarYear_ReturnsJan1ToDecember31()
    {
        var (start, end) = DateRangeHelper.GetDateRange(PeriodType.CalendarYear);

        var now = DateTime.Now;
        start.Should().Be(new DateTime(now.Year, 1, 1));
        end.Year.Should().Be(now.Year);
        end.Month.Should().Be(12);
        end.Day.Should().Be(31);
    }

    [Fact]
    public void Today_ReturnsTodayRange()
    {
        var (start, end) = DateRangeHelper.GetDateRange(PeriodType.Today);

        var now = DateTime.Now;
        start.Should().Be(now.Date);
        end.Should().BeAfter(now.Date);
        end.Should().BeBefore(now.Date.AddDays(1));
    }

    [Fact]
    public void LastWeek_ReturnsLast7DaysRange()
    {
        var (start, end) = DateRangeHelper.GetDateRange(PeriodType.LastWeek);

        var now = DateTime.Now;
        start.Should().Be(now.Date.AddDays(-7));
        end.Should().BeAfter(now.Date);
        end.Should().BeBefore(now.Date.AddDays(1));
    }

    [Fact]
    public void LastYear_ReturnsLastYearRange()
    {
        var (start, end) = DateRangeHelper.GetDateRange(PeriodType.LastYear);

        var now = DateTime.Now;
        start.Should().Be(now.Date.AddYears(-1));
        end.Should().BeAfter(now.Date);
        end.Should().BeBefore(now.Date.AddDays(1));
    }

    [Fact]
    public void LastMonth_ReturnsDefaultCase()
    {
        var (start, end) = DateRangeHelper.GetDateRange(PeriodType.LastMonth);

        var now = DateTime.Now;
        start.Should().Be(now.Date.AddMonths(-1));
        end.Should().BeAfter(now.Date);
    }

    [Fact]
    public void CustomPeriod_ReturnsGivenDates()
    {
        var customStart = new DateTime(2026, 3, 15);
        var customEnd = new DateTime(2026, 4, 20);

        var (start, end) = DateRangeHelper.GetDateRange(PeriodType.CustomPeriod, customStart, customEnd);

        start.Should().Be(customStart.Date);
        end.Should().BeAfter(customEnd.Date);
        end.Should().BeBefore(customEnd.Date.AddDays(1));
    }

    [Fact]
    public void NullPeriodType_ReturnsLastMonthDefault()
    {
        var (start, end) = DateRangeHelper.GetDateRange(null);

        var now = DateTime.Now;
        start.Should().Be(now.Date.AddMonths(-1));
    }

    [Fact]
    public void StartDate_AlwaysBeforeEndDate()
    {
        foreach (PeriodType period in Enum.GetValues<PeriodType>())
        {
            var customStart = new DateTime(2026, 1, 1);
            var customEnd = new DateTime(2026, 12, 31);
            var (start, end) = DateRangeHelper.GetDateRange(period, customStart, customEnd);

            start.Should().BeBefore(end, $"PeriodType.{period} should have start < end");
        }
    }

    [Theory]
    [InlineData(BudgetPeriod.Day, "2024-02-29", "2024-03-01")]
    [InlineData(BudgetPeriod.Month, "2024-02-01", "2024-03-01")]
    [InlineData(BudgetPeriod.Year, "2024-01-01", "2025-01-01")]
    public void GetBudgetWindow_covers_the_whole_period_up_to_its_last_tick(BudgetPeriod period, string start, string nextStart)
    {
        var (startDate, endDate) = DateRangeHelper.GetBudgetWindow(period, new DateTime(2024, 2, 29, 18, 30, 0));

        startDate.Should().Be(DateTime.Parse(start, System.Globalization.CultureInfo.InvariantCulture));
        endDate.Should().Be(DateTime.Parse(nextStart, System.Globalization.CultureInfo.InvariantCulture).AddTicks(-1));
    }
}
