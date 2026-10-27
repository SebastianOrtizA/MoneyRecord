using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using System.Globalization;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

/// <summary>ReceiptReader: the total and date found in a receipt's OCR text.</summary>
public sealed class ReceiptReaderTests
{
    private static readonly DateTime Today = new(2026, 10, 16);
    private static readonly CultureInfo Spain = CultureInfo.GetCultureInfo("es-ES");
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    private static ReceiptReading Read(CultureInfo culture, params string[] rows) => ReceiptReader.Read(rows, Today, culture);

    // ---- Rows ----

    [Fact]
    public void Lines_side_by_side_become_one_row_left_to_right()
    {
        // Two columns: ML Kit returns the labels' block, then the prices' block
        var rows = ReceiptReader.Rows(
        [
            new ReceiptLine("SUBTOTAL", 10, 100, 120, 120),
            new ReceiptLine("TOTAL", 10, 130, 90, 150),
            new ReceiptLine("10,33", 300, 102, 360, 122),
            new ReceiptLine("12,50", 300, 133, 360, 153),
        ]);

        rows.Should().Equal("SUBTOTAL  10,33", "TOTAL  12,50");
    }

    [Fact]
    public void Two_column_receipt_reads_the_total_row()
    {
        var reading = ReceiptReader.Read(
        [
            new ReceiptLine("PANADERIA LA ESPIGA", 40, 10, 400, 30),
            new ReceiptLine("14/10/2026 09:12", 40, 40, 300, 60),
            new ReceiptLine("Pan", 10, 80, 60, 100),
            new ReceiptLine("Café", 10, 110, 60, 130),
            new ReceiptLine("TOTAL", 10, 160, 90, 180),
            new ReceiptLine("EFECTIVO", 10, 190, 110, 210),
            new ReceiptLine("CAMBIO", 10, 220, 100, 240),
            new ReceiptLine("1,20", 300, 81, 360, 101),
            new ReceiptLine("1,50", 300, 112, 360, 132),
            new ReceiptLine("2,70", 300, 162, 360, 182),
            new ReceiptLine("5,00", 300, 191, 360, 211),
            new ReceiptLine("2,30", 300, 221, 360, 241),
        ], Today, Spain);

        reading.Amount.Should().Be(2.70m);
        reading.Date.Should().Be(new DateTime(2026, 10, 14));
    }

    // ---- Total ----

    [Theory]
    [InlineData("TOTAL 12,50", 12.50)]
    [InlineData("Total: $1,234.56", 1234.56)]
    [InlineData("TOTAL EUR 1.234,56", 1234.56)]
    [InlineData("Total a pagar $ 25.000", 25000)]
    [InlineData("TOTAL 25000", 25000)]
    [InlineData("Importe 9.99€", 9.99)]
    [InlineData("AMOUNT DUE  18.7", 18.70)]
    [InlineData("Total (IVA incluido)  7,80", 7.80)]
    public void Total_row_gives_the_amount(string row, double expected) =>
        Read(Spain, "Item 3,00", row).Amount.Should().Be((decimal)expected);

    [Fact]
    public void Subtotal_tax_cash_and_change_are_not_the_total() =>
        Read(Us,
            "Subtotal 20.00",
            "Tax 1.60",
            "TOTAL 21.60",
            "Cash 50.00",
            "Change 28.40",
            "Total items 3").Amount.Should().Be(21.60m);

    [Fact]
    public void A_stronger_total_wins() =>
        Read(Spain,
            "Total 30,00",
            "Descuento -5,00",
            "Total a pagar 25,00").Amount.Should().Be(25.00m);

    [Fact]
    public void Among_equal_totals_the_largest_wins() =>
        Read(Us, "Total 5.00", "Total 45.00").Amount.Should().Be(45.00m, "e.g. a per-section total before the receipt's");

    [Fact]
    public void Without_a_total_row_the_largest_price_is_used() =>
        Read(Us, "Coffee 3.50", "Sandwich 8.25", "Table 12", "Visa 11.75").Amount.Should().Be(11.75m);

    [Fact]
    public void Percentages_codes_and_negative_amounts_are_ignored() =>
        Read(Spain, "IVA 21%", "NIF B12345678", "Ref 123456789012", "-40,00", "Pan 1,20").Amount.Should().Be(1.20m);

    [Fact]
    public void Item_that_looks_like_an_old_date_keeps_its_price()
    {
        // "1 MAYO 12" reads as 1 May 2012, too old for a receipt: the row is an item and 12,50 its price
        var reading = Read(Spain, "1 MAYO 12,50", "Pan 1,20");

        reading.Amount.Should().Be(12.50m);
        reading.Date.Should().BeNull();
    }

    [Fact]
    public void Nothing_readable_gives_nothing()
    {
        var reading = Read(Us, "THANK YOU", "Please come again");

        reading.IsEmpty.Should().BeTrue();
    }

    // ---- Date ----

    [Theory]
    [InlineData("Fecha: 14/10/2026", 2026, 10, 14)]
    [InlineData("14.10.26 13:05", 2026, 10, 14)]
    [InlineData("2026-10-14T13:05", 2026, 10, 14)]
    [InlineData("05/04/2026", 2026, 4, 5)]
    [InlineData("14 oct 2026", 2026, 10, 14)]
    [InlineData("14-OCT-26", 2026, 10, 14)]
    [InlineData("14 de octubre de 2026", 2026, 10, 14)]
    [InlineData("Oct 14, 2026", 2026, 10, 14)]
    [InlineData("1 sept. 2026", 2026, 9, 1)]
    public void Date_layouts_are_read_day_first_in_spain(string row, int year, int month, int day) =>
        Read(Spain, row).Date.Should().Be(new DateTime(year, month, day));

    [Fact]
    public void Ambiguous_date_follows_the_culture_order()
    {
        Read(Us, "05/04/2026").Date.Should().Be(new DateTime(2026, 5, 4));
        Read(Spain, "05/04/2026").Date.Should().Be(new DateTime(2026, 4, 5));
    }

    [Fact]
    public void Ambiguous_date_in_the_future_takes_the_other_order() =>
        Read(Us, "12/10/2026").Date.Should().Be(new DateTime(2026, 10, 12), "December 10 hasn't come yet");

    [Fact]
    public void Future_and_very_old_dates_are_skipped() =>
        Read(Spain, "Válido hasta 31/12/2027", "Cliente desde 01/01/2001", "Fecha 02/10/2026").Date
            .Should().Be(new DateTime(2026, 10, 2));

    [Theory]
    [InlineData("fr-FR", "Sous-total 20,00", "TVA 2,00", "TOTAL TTC 22,00", "Espèces 50,00", "Rendu 28,00", "14 oct. 2026")]
    [InlineData("it-IT", "Subtotale 20,00", "IVA 2,00", "TOTALE 22,00", "Contanti 50,00", "Resto 28,00", "14 ottobre 2026")]
    [InlineData("pt-BR", "Subtotal 20,00", "ICMS 2,00", "TOTAL 22,00", "Dinheiro 50,00", "Troco 28,00", "14 de outubro de 2026")]
    public void Receipts_in_French_Italian_and_Portuguese_read_the_total_and_date(
        string culture, string subtotal, string tax, string total, string cash, string change, string date)
    {
        var reading = Read(CultureInfo.GetCultureInfo(culture), "Boulangerie", date, subtotal, tax, total, cash, change);

        reading.Amount.Should().Be(22.00m);
        reading.Date.Should().Be(new DateTime(2026, 10, 14));
    }

    [Fact]
    public void Dates_and_times_are_not_read_as_amounts()
    {
        var reading = Read(Spain, "14.10.2026 13:05", "Café 1,50");

        reading.Amount.Should().Be(1.50m);
        reading.Date.Should().Be(new DateTime(2026, 10, 14));
    }
}
