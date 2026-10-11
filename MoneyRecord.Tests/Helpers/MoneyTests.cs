using FluentAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using System.Text.Json;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class MoneyTests
{
    [Theory]
    [InlineData("12.34", 1234)]
    [InlineData("0.005", 1)]
    [InlineData("-0.005", -1)]
    [InlineData("-7.125", -713)]
    [InlineData("1000000", 100000000)]
    public void ToCents_RoundsHalfAwayFromZero(string amount, long expected)
    {
        Money.ToCents(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)).Should().Be(expected);
    }

    [Fact]
    public void FromCents_IsExact()
    {
        Money.FromCents(1234).Should().Be(12.34m);
        Money.FromCents(-5).Should().Be(-0.05m);
    }

    [Fact]
    public void ModelAmount_IsBackedByCents()
    {
        var transaction = new Transaction { Amount = 0.1m + 0.2m };

        transaction.AmountCents.Should().Be(30);
        transaction.Amount.Should().Be(0.3m);
    }

    [Fact]
    public void BackupV1Json_WithDecimalsOnly_Imports()
    {
        var account = JsonSerializer.Deserialize<Account>("{\"Name\":\"Bank\",\"InitialBalance\":1250.5}")!;
        var budget = JsonSerializer.Deserialize<Budget>("{\"LimitAmount\":300}")!;

        account.InitialBalanceCents.Should().Be(125050);
        budget.LimitAmountCents.Should().Be(30000);
    }

    [Fact]
    public void BackupV2Json_RoundTripsExactly()
    {
        var original = new Transfer { Amount = 99.99m };

        var copy = JsonSerializer.Deserialize<Transfer>(JsonSerializer.Serialize(original))!;

        copy.AmountCents.Should().Be(9999);
    }

    [Fact]
    public void BackupJson_CentsWinOverDecimal()
    {
        // The cents property is declared (and so serialized) after the decimal one
        var transaction = JsonSerializer.Deserialize<Transaction>("{\"Amount\":12.339999,\"AmountCents\":1234}")!;

        transaction.Amount.Should().Be(12.34m);
    }
}
