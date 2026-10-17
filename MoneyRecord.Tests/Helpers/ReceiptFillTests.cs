using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

/// <summary>ReceiptFill: what a receipt reading changes in the add form.</summary>
public sealed class ReceiptFillTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 18, 30, 0);
    private static readonly ReceiptReading Receipt = new(4.10m, new DateTime(2026, 10, 14));

    [Fact]
    public void New_entry_gets_the_total_and_the_date_keeping_the_time()
    {
        var fill = ReceiptFill.Apply(Receipt, typedCents: null, isNewEntry: true, Now);

        fill.Should().Be(new ReceiptFill(4.10m, new DateTime(2026, 10, 14, 18, 30, 0), null));
    }

    [Fact]
    public void Zero_counts_as_empty() =>
        ReceiptFill.Apply(Receipt, typedCents: 0, isNewEntry: true, Now).Amount.Should().Be(4.10m);

    [Fact]
    public void Same_amount_typed_changes_only_the_date()
    {
        var fill = ReceiptFill.Apply(Receipt, typedCents: 410, isNewEntry: true, Now);

        fill.Amount.Should().BeNull();
        fill.Differs.Should().BeNull();
        fill.Date.Should().Be(new DateTime(2026, 10, 14, 18, 30, 0));
    }

    [Fact]
    public void Different_amount_typed_is_kept_mentioned_and_the_date_left_alone()
    {
        var fill = ReceiptFill.Apply(Receipt, typedCents: 500, isNewEntry: true, Now);

        fill.Should().Be(new ReceiptFill(null, null, 4.10m));
        fill.ChangesNothing.Should().BeTrue();
    }

    [Fact]
    public void Edited_entry_keeps_its_date() =>
        ReceiptFill.Apply(Receipt, typedCents: 410, isNewEntry: false, Now).ChangesNothing.Should().BeTrue();

    [Fact]
    public void Date_already_right_is_no_change() =>
        ReceiptFill.Apply(Receipt with { Amount = null }, typedCents: null, isNewEntry: true, new DateTime(2026, 10, 14, 9, 0, 0))
            .ChangesNothing.Should().BeTrue();

    [Fact]
    public void Nothing_read_changes_nothing() =>
        ReceiptFill.Apply(ReceiptReading.None, typedCents: null, isNewEntry: true, Now)
            .Should().Be(new ReceiptFill(null, null, null));
}
