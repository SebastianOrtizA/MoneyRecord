using AwesomeAssertions;
using MoneyRecord.Helpers;
using System.Globalization;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

/// <summary>
/// The thousands separators put in an amount while it is typed, and where the caret goes.
/// </summary>
public class AmountMaskTests
{
    private static readonly CultureInfo En = new("en-US");
    private static readonly CultureInfo Es = new("es-CO");

    private static (string Text, int Caret) Group(string text, string? old, CultureInfo culture, int decimals = 2, bool negative = false) =>
        AmountMask.Group(text, old, decimals, negative, culture);

    [Fact]
    public void Typing_digits_at_the_end_groups_them_and_keeps_the_caret_at_the_end()
    {
        var text = "";
        foreach (var typed in new[] { "2", "25", "254", "2543", "25432" })
        {
            var result = Group(typed.Length == 1 ? typed : text + typed[^1], text, Es);
            text = result.Text;
            result.Caret.Should().Be(text.Length);
        }

        text.Should().Be("25.432");
    }

    [Fact]
    public void Group_separator_follows_the_culture()
    {
        Group("1250000", "125000", En).Text.Should().Be("1,250,000");
        Group("1250000", "125000", Es).Text.Should().Be("1.250.000");
    }

    [Fact]
    public void Typing_in_the_middle_keeps_the_caret_next_to_the_typed_digit()
    {
        // "1,234" with a 9 typed after the 2 → "1,2934" → "12,934"; caret after the 9
        var result = Group("1,2934", "1,234", En);

        result.Text.Should().Be("12,934");
        result.Caret.Should().Be(4);
    }

    [Fact]
    public void Deleting_a_digit_regroups_and_keeps_the_caret()
    {
        // "12,934" with the 9 deleted → "12,34" → "1,234"; caret where the 9 was
        var result = Group("12,34", "12,934", En);

        result.Text.Should().Be("1,234");
        result.Caret.Should().Be(3);
    }

    [Fact]
    public void Deleting_a_separator_deletes_nothing_visible()
    {
        // Backspace on the comma of "1,234" → "1234" → "1,234": the caret moves left of the comma
        var result = Group("1234", "1,234", En);

        result.Text.Should().Be("1,234");
        result.Caret.Should().Be(1);
    }

    [Fact]
    public void Decimals_are_kept_and_limited_to_the_currency()
    {
        Group("1,250.5", "1,250.", En).Text.Should().Be("1,250.5");
        Group("1,250.567", "1,250.56", En).Text.Should().Be("1,250.56");
        Group("1.250,5", "1.250,", Es).Text.Should().Be("1.250,5");
    }

    [Fact]
    public void A_trailing_decimal_mark_stays()
    {
        var result = Group("1250.", "1250", En);

        result.Text.Should().Be("1,250.");
        result.Caret.Should().Be(6);
    }

    [Fact]
    public void A_currency_without_decimals_takes_no_decimal_mark()
    {
        var result = Group("1500.", "1500", En, decimals: 0);

        result.Text.Should().Be("1,500");
    }

    [Fact]
    public void Leading_zeros_are_dropped_but_a_single_zero_stays()
    {
        Group("05", "0", En).Text.Should().Be("5");
        Group("0.5", "0", En).Text.Should().Be("0.5");
        Group("00", "0", En).Text.Should().Be("0");
    }

    [Fact]
    public void A_minus_sign_is_kept_only_when_negative_values_are_allowed()
    {
        Group("-1250", "-125", En, negative: true).Text.Should().Be("-1,250");
        Group("-1250", "-125", En).Text.Should().Be("1,250");
    }
}
