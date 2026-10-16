using AwesomeAssertions;
using MoneyRecord.Helpers;
using System.Globalization;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class DateFormatterTests
{
    private static readonly DateTime Date = new(2026, 10, 11);

    [Fact]
    public void EnUs_UsesMonthFirst()
    {
        var culture = new CultureInfo("en-US");

        DateFormatter.Format(Date, DateStyle.Long, culture).Should().Be("October 11, 2026");
        DateFormatter.Format(Date, DateStyle.Medium, culture).Should().Be("Oct 11, 2026");
        DateFormatter.Format(Date, DateStyle.MonthDay, culture).Should().Be("Oct 11");
        DateFormatter.Format(Date, DateStyle.Month, culture).Should().Be("Oct");
        DateFormatter.Format(Date, DateStyle.MonthYear, culture).Should().Be("Oct 2026");
    }

    [Theory]
    [InlineData("es-ES")]
    [InlineData("es-CO")]
    public void Spanish_UsesDayFirstWithDe(string name)
    {
        var culture = new CultureInfo(name);

        DateFormatter.Format(Date, DateStyle.Long, culture).Should().Be("11 de octubre de 2026");
        DateFormatter.Format(Date, DateStyle.Medium, culture).Should().StartWith("11 de oct").And.EndWith("2026");
        DateFormatter.Format(Date, DateStyle.MonthDay, culture).Should().StartWith("11 de oct");
        DateFormatter.Format(Date, DateStyle.Month, culture).Should().StartWith("oct");
        DateFormatter.Format(Date, DateStyle.MonthYear, culture).Should().StartWith("oct").And.EndWith("2026");
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("en-GB")]
    [InlineData("es-ES")]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("ja-JP")]
    public void Patterns_NeverShowTheWeekday(string name)
    {
        var culture = new CultureInfo(name);
        var weekday = culture.DateTimeFormat.GetDayName(Date.DayOfWeek);

        foreach (var style in Enum.GetValues<DateStyle>())
        {
            var text = DateFormatter.Format(Date, style, culture);
            text.Should().NotContainEquivalentOf(weekday);
            text.Should().NotStartWith(",").And.NotEndWith(",");
        }
    }
}
