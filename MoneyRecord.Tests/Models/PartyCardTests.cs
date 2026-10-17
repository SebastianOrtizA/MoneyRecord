using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Models;

[Collection(LanguageCollection.Name)]
public sealed class PartyCardTests : LanguageTestBase
{
    private static PartyCard Card(long spentCents, long? budgetCents) =>
        new(new PartySummary(new PartyEvent { Name = "Birthday", BudgetCents = budgetCents, CurrencyCode = "USD" }, spentCents, [], []));

    [Fact]
    public void WithoutBudget_NoProgressOrStatus()
    {
        var card = Card(2500, null);

        card.HasBudget.Should().BeFalse();
        card.StatusText.Should().BeEmpty();
        card.Level.Should().Be(BudgetLevel.OnTrack);
    }

    [Fact]
    public void UnderBudget_ShowsWhatIsLeft()
    {
        var card = Card(2500, 10000);

        card.HasBudget.Should().BeTrue();
        card.ProgressPercentage.Should().Be(25);
        card.StatusText.Should().Contain("75");
    }

    [Fact]
    public void OverBudget_ShowsHowMuchOverAndCapsTheBar()
    {
        var card = Card(12500, 10000);

        card.ProgressPercentage.Should().Be(100);
        card.Level.Should().Be(BudgetLevel.Exceeded);
        card.StatusText.Should().Contain("25");
    }

    [Fact]
    public void BalanceRow_OwedOwesAndEven()
    {
        new PartyBalanceRow(new PartyBalance(1, "Ana", 3000, 0, 1000), "USD").IsOwed.Should().BeTrue();
        new PartyBalanceRow(new PartyBalance(2, "Ben", 0, 0, 500), "USD").Owes.Should().BeTrue();
        var even = new PartyBalanceRow(new PartyBalance(3, "Cam", 500, 0, 500), "USD");
        even.IsOwed.Should().BeFalse();
        even.Owes.Should().BeFalse();
    }

    [Fact]
    public void BalanceRow_PotLineIsNamedPot()
    {
        var pot = new PartyBalanceRow(new PartyBalance(PartyLedger.PotId, string.Empty, 0, -600, 0), "USD");

        pot.IsPot.Should().BeTrue();
        pot.Name.Should().NotBeEmpty();
        pot.Owes.Should().BeTrue();
    }
}
