using Xunit;
using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Services;

namespace MoneyRecord.Tests.Models;

public class HomeLayoutTests
{
    [Fact]
    public void Parse_empty_gives_the_default()
    {
        var layout = HomeLayout.Parse(null);

        layout.Order.Should().Equal(HomeCard.Balance, HomeCard.GetStarted, HomeCard.PeriodSummary, HomeCard.Recent,
            HomeCard.Upcoming, HomeCard.Budgets, HomeCard.Trip, HomeCard.Goals, HomeCard.Debts);
        layout.Hidden.Should().BeEquivalentTo([HomeCard.Debts]);
    }

    [Fact]
    public void Serialize_and_parse_round_trip()
    {
        var layout = new HomeLayout([HomeCard.Goals, HomeCard.Balance, HomeCard.Recent, HomeCard.PeriodSummary,
            HomeCard.Upcoming, HomeCard.Budgets, HomeCard.Trip, HomeCard.Debts, HomeCard.GetStarted], new HashSet<HomeCard> { HomeCard.Budgets, HomeCard.Trip });

        var parsed = HomeLayout.Parse(layout.Serialize());

        parsed.Order.Should().Equal(layout.Order);
        parsed.Hidden.Should().BeEquivalentTo(layout.Hidden);
    }

    [Fact]
    public void Unknown_and_repeated_names_are_dropped()
    {
        var layout = HomeLayout.Parse("Recent,Nope,99,Recent,Balance;hidden=Nope,Goals");

        layout.Order.Take(2).Should().Equal(HomeCard.Recent, HomeCard.Balance);
        layout.Order.Should().OnlyHaveUniqueItems().And.HaveCount(Enum.GetValues<HomeCard>().Length);
        layout.Hidden.Should().BeEquivalentTo([HomeCard.Goals, HomeCard.Debts]);
    }

    [Fact]
    public void Cards_missing_from_a_saved_layout_are_appended_with_their_default_visibility()
    {
        var layout = HomeLayout.Parse("Goals,Balance;hidden=Goals");

        layout.Order.Should().Equal([HomeCard.Goals, HomeCard.Balance, HomeCard.PeriodSummary, HomeCard.Recent,
            HomeCard.Upcoming, HomeCard.Budgets, HomeCard.Trip, HomeCard.Debts, HomeCard.GetStarted]);
        layout.IsShown(HomeCard.GetStarted).Should().BeTrue();
        layout.IsShown(HomeCard.Debts).Should().BeFalse();
        layout.IsShown(HomeCard.Trip).Should().BeTrue();
        layout.IsShown(HomeCard.Goals).Should().BeFalse();
    }

    [Fact]
    public void Debts_card_is_opt_in()
    {
        HomeLayout.Default.IsShown(HomeCard.Debts).Should().BeFalse();
        HomeLayout.Parse("Debts,Balance").IsShown(HomeCard.Debts).Should().BeTrue();
    }

    [Fact]
    public void Balance_is_never_hidden()
    {
        HomeLayout.Parse("Balance;hidden=Balance,Recent").IsShown(HomeCard.Balance).Should().BeTrue();
    }

    [Fact]
    public void Settings_save_and_read_the_layout()
    {
        var settings = new HomeLayoutSettings(new FakePreferences());
        settings.Layout.Should().BeEquivalentTo(HomeLayout.Default);

        settings.Layout = new HomeLayout([HomeCard.Trip, .. Enum.GetValues<HomeCard>().Where(c => c != HomeCard.Trip)],
            new HashSet<HomeCard> { HomeCard.Recent });

        settings.Layout.Order[0].Should().Be(HomeCard.Trip);
        settings.Layout.IsShown(HomeCard.Recent).Should().BeFalse();
    }
}
