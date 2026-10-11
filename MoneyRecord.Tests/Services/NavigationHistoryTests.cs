using AwesomeAssertions;
using MoneyRecord.Services;
using Xunit;

namespace MoneyRecord.Tests.Services;

public class NavigationHistoryTests
{
    [Theory]
    [InlineData("//MainPage", true)]
    [InlineData("//BudgetPage", true)]
    [InlineData("//TrendsReportPage", true)]
    [InlineData("//RecurringTransactionsPage", true)]
    [InlineData("//SettingsPage", true)]
    [InlineData("//ManageIncomeCategories", true)]
    [InlineData("//MainPage?x=1", true)]
    [InlineData("//MainPage/AddTransactionPage", false)]
    [InlineData("//MainPage/AddTransferPage?TransferId=5", false)]
    [InlineData("AddTransactionPage", false)]
    [InlineData("//", false)]
    [InlineData("", false)]
    public void IsRoot_is_true_only_for_flyout_pages(string location, bool expected)
    {
        NavigationHistory.IsRoot(location).Should().Be(expected);
    }

    [Fact]
    public void Every_flyout_page_clears_the_history()
    {
        // 7.2 fix: BudgetPage, TrendsReportPage, RecurringTransactionsPage and SettingsPage used to pile up
        var history = new NavigationHistory();
        history.Record("//MainPage");
        history.Record("//BudgetPage");
        history.Record("//TrendsReportPage");
        history.Record("//RecurringTransactionsPage");
        history.Record("//SettingsPage");

        history.CanGoBack.Should().BeFalse();
        history.CurrentLocation.Should().Be("//SettingsPage");
    }

    [Fact]
    public void Leaving_a_pushed_page_pops_it_with_a_relative_route()
    {
        var history = new NavigationHistory();
        history.Record("//ExpenseReportPage");
        history.Record("//ExpenseReportPage/AddTransactionPage");

        history.CanGoBack.Should().BeTrue();
        history.BeginGoBack().Should().Be("..");

        history.Record("//ExpenseReportPage");
        history.CanGoBack.Should().BeFalse();
        history.CurrentLocation.Should().Be("//ExpenseReportPage");
    }

    [Fact]
    public void Going_back_from_a_flyout_page_without_history_goes_home()
    {
        var history = new NavigationHistory();
        history.Record("//BudgetPage");

        history.BeginGoBack().Should().Be(NavigationHistory.HomeRoute);

        history.Record("//MainPage");
        history.CanGoBack.Should().BeFalse();
    }

    [Fact]
    public void The_location_reached_by_going_back_is_not_pushed_again()
    {
        var history = new NavigationHistory();
        history.Record("//MainPage");
        history.Record("//MainPage/AddTransactionPage");
        history.BeginGoBack();
        history.Record("//MainPage");

        // A second visit records one entry, not two
        history.Record("//MainPage/AddTransactionPage");
        history.BeginGoBack().Should().Be("..");
        history.Record("//MainPage");
        history.CanGoBack.Should().BeFalse();
    }

    [Theory]
    [InlineData("//MainPage", true)]
    [InlineData("//mainpage", true)]
    [InlineData("//MainPage/AddTransactionPage", false)]
    [InlineData("//BudgetPage", false)]
    public void IsHome_matches_only_the_home_page(string location, bool expected)
    {
        NavigationHistory.IsHome(location).Should().Be(expected);
    }
}
