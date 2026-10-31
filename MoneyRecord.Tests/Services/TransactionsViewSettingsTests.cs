using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Services;
using Xunit;

namespace MoneyRecord.Tests.Services;

public class TransactionsViewSettingsTests
{
    private readonly FakePreferences _preferences = new();

    [Fact]
    public void The_list_is_grouped_by_date_until_the_user_picks_another()
    {
        new TransactionsViewSettings(_preferences).Grouping.Should().Be(GroupingMode.Date);
    }

    [Theory]
    [InlineData(GroupingMode.None)]
    [InlineData(GroupingMode.Category)]
    [InlineData(GroupingMode.Account)]
    [InlineData(GroupingMode.Date)]
    public void A_pick_is_remembered(GroupingMode mode)
    {
        new TransactionsViewSettings(_preferences).Grouping = mode;

        new TransactionsViewSettings(_preferences).Grouping.Should().Be(mode);
    }

    [Fact]
    public void An_unknown_saved_value_falls_back_to_date()
    {
        _preferences.Set("Transactions.Grouping", "ByWeek");
        new TransactionsViewSettings(_preferences).Grouping.Should().Be(GroupingMode.Date);

        _preferences.Set("Transactions.Grouping", "42");
        new TransactionsViewSettings(_preferences).Grouping.Should().Be(GroupingMode.Date);
    }
}
