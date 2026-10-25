using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public sealed class GoalLinkChoicesTests
{
    private static Account Account(int id, string name, string currency = "USD") =>
        new() { Id = id, Name = name, CurrencyCode = currency };

    private static PocketSummary Pocket(int id, Account account, string name) =>
        new(new Pocket { Id = id, AccountId = account.Id, Name = name }, account, 0);

    private static readonly Dictionary<int, int> None = [];

    [Fact]
    public void Each_account_is_followed_by_its_pockets()
    {
        var bank = Account(1, "Bank");
        var cash = Account(2, "Cash");

        var options = GoalLinkChoices.Build([bank, cash], [Pocket(10, bank, "Travel"), Pocket(11, bank, "Rainy")], None, None, null, "USD");

        options.Select(o => o.Name).Should().Equal("Bank", "Bank › Travel", "Bank › Rainy", "Cash");
        options.Select(o => o.IsPocket).Should().Equal(false, true, true, false);
    }

    [Fact]
    public void Only_the_goals_currency_is_offered()
    {
        var bank = Account(1, "Bank");
        var euros = Account(2, "Euros", "EUR");

        var options = GoalLinkChoices.Build([bank, euros], [Pocket(10, euros, "Trip")], None, None, null, "USD");

        options.Select(o => o.Name).Should().Equal("Bank");
    }

    [Fact]
    public void An_account_of_another_goal_is_hidden_but_its_free_pockets_stay()
    {
        var bank = Account(1, "Bank");
        var accounts = new Dictionary<int, int> { [1] = 7 };
        var pockets = new Dictionary<int, int> { [11] = 7 };

        var options = GoalLinkChoices.Build([bank], [Pocket(10, bank, "Travel"), Pocket(11, bank, "Rainy")], accounts, pockets, 3, "USD");

        options.Select(o => o.Name).Should().Equal("Bank › Travel");
    }

    [Fact]
    public void The_goal_being_edited_keeps_its_own_links_offered()
    {
        var bank = Account(1, "Bank");
        var accounts = new Dictionary<int, int> { [1] = 3 };
        var pockets = new Dictionary<int, int> { [10] = 3 };

        var options = GoalLinkChoices.Build([bank], [Pocket(10, bank, "Travel")], accounts, pockets, 3, "USD");

        options.Select(o => o.Name).Should().Equal("Bank", "Bank › Travel");
    }

    [Fact]
    public void An_account_and_its_own_pockets_exclude_each_other_but_pockets_go_together()
    {
        var bank = Account(1, "Bank");
        var cash = Account(2, "Cash");
        var account = new GoalLinkOption(bank, null);
        var travel = new GoalLinkOption(bank, Pocket(10, bank, "Travel"));
        var rainy = new GoalLinkOption(bank, Pocket(11, bank, "Rainy"));
        var cashPocket = new GoalLinkOption(cash, Pocket(12, cash, "Kept"));

        account.ExcludesOther(travel).Should().BeTrue();
        travel.ExcludesOther(account).Should().BeTrue();
        travel.ExcludesOther(rainy).Should().BeFalse();
        account.ExcludesOther(cashPocket).Should().BeFalse("it belongs to another account");
    }
}
