using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class ChallengeSourcesTests
{
    private static string Base => MoneyFormatter.BaseCurrency;

    private static readonly Account Bank = new() { Id = 1, Name = "Bank", CurrencyCode = Base };
    private static readonly Account Savings = new() { Id = 2, Name = "Savings", CurrencyCode = Base };
    private static readonly Account Dollars = new() { Id = 3, Name = "Dollars", CurrencyCode = "ZZZ" };

    private static PocketSummary Pocket(int id, string name, Account account, long cents = 0) =>
        new(new Pocket { Id = id, AccountId = account.Id, Name = name }, account, cents);

    [Theory]
    [InlineData(1, null, 2, null, true)]   // account to another account
    [InlineData(1, 10, 2, null, true)]     // pocket to another account
    [InlineData(1, null, 1, 11, true)]     // account to its own pocket
    [InlineData(1, 10, 1, 11, true)]       // pocket to another pocket of the same account
    [InlineData(1, 10, 1, 10, false)]      // a pocket to itself
    [InlineData(1, null, 1, null, false)]  // an account to itself
    [InlineData(1, 10, 1, null, false)]    // a pocket back to its own account: no balance changes
    public void A_route_is_valid_when_money_actually_moves(int sourceAccount, int? sourcePocket, int destinationAccount, int? destinationPocket, bool expected) =>
        ChallengeSources.IsValidRoute(sourceAccount, sourcePocket, destinationAccount, destinationPocket).Should().Be(expected);

    [Fact]
    public void Destinations_offer_the_accounts_and_pockets_in_the_challenge_currency()
    {
        var pockets = new[] { Pocket(10, "Travel", Bank, 2_500), Pocket(20, "Rain", Dollars) };

        var options = ChallengeSources.Destinations(Base, [Bank, Savings, Dollars], pockets);

        options.Select(o => o.Place).Should().Equal(ChallengePlace.Account(1), ChallengePlace.Account(2), ChallengePlace.Pocket(10));
        options[2].Label.Should().StartWith("Bank › Travel (");
    }

    [Fact]
    public void Sources_leave_out_the_destination_account_unless_it_is_a_pocket_and_never_that_pocket()
    {
        var food = Pocket(10, "Food", Bank, 100);
        var holiday = Pocket(11, "Holiday", Bank);
        var other = Pocket(20, "Other", Savings);
        var challenge = new SavingsChallenge { CurrencyCode = Base, ToAccountId = 1, ToPocketId = 11 };

        var places = ChallengeSources.Sources(challenge, [Bank, Savings], [food, holiday, other]).Select(o => o.Place);

        places.Should().Equal(ChallengePlace.Account(1), ChallengePlace.Account(2), ChallengePlace.Pocket(10), ChallengePlace.Pocket(20));
    }

    [Fact]
    public void Sources_for_a_destination_account_are_the_other_accounts_and_their_pockets()
    {
        var food = Pocket(10, "Food", Bank, 100);
        var other = Pocket(20, "Other", Savings);
        var challenge = new SavingsChallenge { CurrencyCode = Base, ToAccountId = 1 };

        var places = ChallengeSources.Sources(challenge, [Bank, Savings], [food, other]).Select(o => o.Place);

        places.Should().Equal(ChallengePlace.Account(2), ChallengePlace.Pocket(20));
    }

    [Fact]
    public void A_challenge_without_a_destination_has_no_sources() =>
        ChallengeSources.Sources(new SavingsChallenge { CurrencyCode = Base }, [Bank], []).Should().BeEmpty();
}
