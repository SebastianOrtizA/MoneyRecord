using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Models;

public class AccountGroupTests
{
    private static Account Account(int id, string currency, decimal initial = 0m) =>
        new() { Id = id, Name = $"Account {id}", CurrencyCode = currency, InitialBalance = initial };

    private static AccountBalanceInfo Balance(int id, decimal amount, DateTime? lastActivity = null) =>
        new() { AccountId = id, CurrentBalance = amount, LastActivityDate = lastActivity };

    [Fact]
    public void Groups_by_currency_with_the_base_first_and_subtotals_in_each_currency()
    {
        var groups = AccountGroup.Build(
            [Account(1, "EUR"), Account(2, "USD"), Account(3, ""), Account(4, "COP"), Account(5, "eur")],
            [Balance(1, 100m), Balance(2, 1000m), Balance(3, 250m), Balance(4, 450000m), Balance(5, 200m)],
            "usd");

        groups.Select(g => (g.CurrencyCode, g.Subtotal, g.Count)).Should().Equal(
            ("USD", 1250m, 2),
            ("COP", 450000m, 1),
            ("EUR", 300m, 2));
        // Accounts keep their order inside a group; one without a currency shows the base currency's code
        groups[0].Select(i => (i.Account.Id, i.CurrencyCode)).Should().Equal((2, "USD"), (3, "USD"));
    }

    [Fact]
    public void Header_shows_code_subtotal_and_count()
    {
        var group = AccountGroup.Build([Account(1, "EUR"), Account(2, "EUR")], [Balance(1, 100m), Balance(2, 200m)], "USD")
            .Single();

        group.HeaderText.Should().Be($"{MoneyFormatter.FormatWithCode(300m, "EUR")} · {Plurals.Accounts(2)}");
    }

    [Fact]
    public void Currency_filter_keeps_only_that_group()
    {
        var groups = AccountGroup.Build([Account(1, "EUR"), Account(2, "USD"), Account(3, "")],
            [Balance(1, 1m), Balance(2, 2m), Balance(3, 3m)], "USD", currencyFilter: "USD");

        groups.Should().ContainSingle().Which.Select(i => i.Account.Id).Should().Equal(2, 3);
    }

    [Fact]
    public void Currency_filter_without_accounts_is_empty()
    {
        AccountGroup.Build([Account(1, "EUR")], [Balance(1, 1m)], "USD", currencyFilter: "JPY").Should().BeEmpty();
    }

    [Fact]
    public void Carries_last_activity_and_falls_back_to_the_initial_balance_without_a_balance_row()
    {
        var date = new DateTime(2026, 10, 11);
        var items = AccountGroup.Build([Account(1, "USD"), Account(2, "USD", initial: 40m)], [Balance(1, 5m, date)], "USD")
            .Single();

        items[0].LastActivityDate.Should().Be(date);
        items[0].LastActivityText.Should().Contain(DateFormatter.Format(date, DateStyle.Medium));
        items[1].CurrentBalance.Should().Be(40m);
        items[1].LastActivityText.Should().BeEmpty();
    }

    private static PocketSummary Pocket(Account account, string name, long cents) =>
        new(new Pocket { Id = 1, AccountId = account.Id, Name = name }, account, cents);

    [Fact]
    public void Pockets_reduce_what_is_available_and_say_so()
    {
        var bank = Account(1, "USD");
        var item = AccountGroup.Build([bank], [Balance(1, 2000m)], "USD",
            pockets: [Pocket(bank, "Travel", 30000), Pocket(bank, "Rainy", 20000)]).Single().Single();

        item.CurrentBalance.Should().Be(2000m);
        item.InPockets.Should().Be(500m);
        item.Available.Should().Be(1500m);
        item.IsOverCommitted.Should().BeFalse();
        item.AvailableText.Should().Be(string.Format(MoneyRecord.Resources.Strings.AppResources.AvailableAndInPockets,
            MoneyFormatter.Format(1500m, "USD"), MoneyFormatter.Format(500m, "USD")));
    }

    [Fact]
    public void Available_goes_negative_when_the_balance_falls_below_the_pockets()
    {
        var bank = Account(1, "USD");
        var item = AccountGroup.Build([bank], [Balance(1, 300m)], "USD", pockets: [Pocket(bank, "Travel", 50000)])
            .Single().Single();

        item.Available.Should().Be(-200m);
        item.IsOverCommitted.Should().BeTrue();
    }

    [Fact]
    public void An_account_without_pockets_has_no_available_text()
    {
        var item = AccountGroup.Build([Account(1, "USD")], [Balance(1, 10m)], "USD").Single().Single();

        item.HasPockets.Should().BeFalse();
        item.AvailableText.Should().BeEmpty();
        item.Available.Should().Be(10m);
    }

    [Fact]
    public void Movement_rows_say_what_happened_with_a_sign()
    {
        var date = new DateTime(2026, 10, 16);
        var setAside = new PocketMovementRow(new PocketMovement { Amount = 100m, Date = date }, "USD");
        var takeOut = new PocketMovementRow(new PocketMovement { Amount = -30m, Date = date, Note = "Taxi" }, "USD");

        setAside.AmountText.Should().Be("+" + MoneyFormatter.Format(100m, "USD"));
        setAside.IsTakeOut.Should().BeFalse();
        takeOut.AmountText.Should().Be("−" + MoneyFormatter.Format(30m, "USD"));
        takeOut.DetailText.Should().EndWith("Taxi");
        takeOut.Title.Should().Be(MoneyRecord.Resources.Strings.AppResources.TakeOut);
    }

    [Fact]
    public void No_accounts_is_no_groups()
    {
        AccountGroup.Build([], [], "USD").Should().BeEmpty();
    }
}
