using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using System.Text;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

/// <summary>Reading bank statements: CsvTable, StatementDate, StatementAmount and the column guess.</summary>
public sealed class StatementParsingTests
{
    // ---- Amounts ----

    [Theory]
    [InlineData("-1,234.56", '.', -1234.56)]
    [InlineData("-1.234,56", ',', -1234.56)]
    [InlineData("$ -12", '.', -12)]
    [InlineData("-$12.50", '.', -12.50)]
    [InlineData("(12.00)", '.', -12)]
    [InlineData("12.00-", '.', -12)]
    [InlineData("+300", '.', 300)]
    [InlineData("1 234,56 €", ',', 1234.56)]
    [InlineData("12.00 USD", '.', 12)]
    [InlineData("1'234.50", '.', 1234.50)]
    [InlineData("−7,5", ',', -7.5)]
    public void Amount_reads_bank_layouts(string text, char separator, double expected)
    {
        StatementAmount.TryParse(text, separator, out var amount).Should().BeTrue();
        amount.Should().Be((decimal)expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Saldo")]
    [InlineData("2026-10-01")]
    [InlineData("12 - 34")]
    [InlineData("1.2.3,4,5")]
    [InlineData("--12")]
    public void Amount_refuses_what_is_not_one_amount(string text) =>
        StatementAmount.TryParse(text, '.', out _).Should().BeFalse();

    [Fact]
    public void Decimal_separator_follows_the_column() =>
        TestCulture.WithCulture("en-US", () =>
        {
            StatementAmount.DetectDecimalSeparator(["-1.234", "-12,50", "300"]).Should().Be(',');
            StatementAmount.DetectDecimalSeparator(["-1,234", "-12.5", "300"]).Should().Be('.');
            StatementAmount.DetectDecimalSeparator(["1.234.567", "-20"]).Should().Be(',');
            StatementAmount.DetectDecimalSeparator(["1.234,00", "5.10"]).Should().Be('.', "a tie keeps the culture's");
        });

    [Fact]
    public void Undecided_column_uses_the_culture()
    {
        TestCulture.WithCulture("es-CO", () => StatementAmount.DetectDecimalSeparator(["1.234", "50"]).Should().Be(','));
        TestCulture.WithCulture("en-US", () => StatementAmount.DetectDecimalSeparator(["1,234", "50"]).Should().Be('.'));
    }

    // ---- Dates ----

    [Fact]
    public void Iso_dates_with_a_time_read_as_the_day()
    {
        var format = StatementDate.Detect(["2026-10-01 14:30:00", "2026-10-02T08:00:00Z", "2026-10-03"]).Should().ContainSingle().Subject;
        format.Pattern.Should().Be("yyyy-MM-dd");
        format.TryParse("2026-10-01 14:30:00", out var date).Should().BeTrue();
        date.Should().Be(new DateTime(2026, 10, 1));
    }

    [Fact]
    public void Ambiguous_day_and_month_offer_both_the_culture_order_first_on_a_tie()
    {
        TestCulture.WithCulture("es-CO", () =>
            StatementDate.Detect(["05/04/2026"]).Select(f => f.Pattern).Should()
                .StartWith(["d/M/yyyy", "M/d/yyyy"]));
        TestCulture.WithCulture("en-US", () =>
            StatementDate.Detect(["05/04/2026"]).Select(f => f.Pattern).Should()
                .StartWith(["M/d/yyyy", "d/M/yyyy"]));
    }

    [Fact]
    public void Ambiguous_dates_prefer_the_shorter_period() =>
        TestCulture.WithCulture("en-US", () =>
            // Day-first: October 1–10; month-first would spread them from January to October
            StatementDate.Detect(["01/10/2026", "05/10/2026", "10/10/2026"]).Select(f => f.Pattern).Should()
                .StartWith(["d/M/yyyy", "M/d/yyyy"]));

    [Fact]
    public void A_day_over_12_settles_the_order() =>
        TestCulture.WithCulture("en-US", () =>
            StatementDate.Detect(["05/04/2026", "25/04/2026"]).Select(f => f.Pattern).Should().Equal("d/M/yyyy"));

    [Fact]
    public void Month_names_read_in_english_and_the_current_language() =>
        TestCulture.WithCulture("es-ES", () =>
        {
            var english = StatementDate.Detect(["01-Oct-2026", "15-Sep-2026"]).Should().ContainSingle().Subject;
            english.TryParse("15-Sep-2026", out var september).Should().BeTrue();
            september.Should().Be(new DateTime(2026, 9, 15));

            var spanish = StatementDate.Detect(["15 sept 2026", "3 oct. 2026"]).Should().ContainSingle().Subject;
            spanish.TryParse("3 oct. 2026", out var october).Should().BeTrue();
            october.Should().Be(new DateTime(2026, 10, 3));
        });

    [Fact]
    public void A_column_with_text_is_not_a_date_column() =>
        StatementDate.Detect(["2026-10-01", "Opening balance"]).Should().BeEmpty();

    // ---- CSV table ----

    [Fact]
    public void Table_skips_the_lines_around_it_and_detects_the_delimiter()
    {
        var table = CsvTable.Parse("""
            Cuenta;0123-4567
            Fecha;Concepto;Importe;Saldo
            01/10/2026;"Compra; Éxito";-45.000,00;1.955.000,00
            02/10/2026;Nómina;2.000.000,00;3.955.000,00

            Total;;1.955.000,00;
            """);
        // The totals line has the table's width, so it stays a row; its date fails and the preview shows it as unreadable
        table.Headers.Should().Equal("Fecha", "Concepto", "Importe", "Saldo");
        table.Rows.Should().HaveCount(3);
        table.Rows[0][1].Should().Be("Compra; Éxito");
    }

    [Fact]
    public void Semicolon_file_with_decimal_commas_and_a_title_line_reads_its_table()
    {
        // Commas in every amount outnumber the semicolons; the delimiter must still be ';'
        var csv = "Banco Ejemplo - Movimientos\r\nCuenta;0123-4567\r\nFecha;Descripción;Referencia;Importe;Saldo\r\n" +
                  "01/10/2026;COMPRA ÉXITO 1234;REF001;-45.000,00;1.955.000,00\r\n" +
                  "02/10/2026;NÓMINA OCTUBRE;REF002;2.000.000,00;3.955.000,00\r\n" +
                  "03/10/2026;UBER TRIP 5555;REF003;-18.500,00;3.936.500,00\r\n" +
                  "Total;;;2.037.800,00;\r\n";
        var table = CsvTable.Read(new MemoryStream(Encoding.Latin1.GetBytes(csv)));

        table.Headers.Should().Equal("Fecha", "Descripción", "Referencia", "Importe", "Saldo");
        TestCulture.WithCulture("en-US", () =>
        {
            var mapping = StatementMapping.Guess(table)!;
            mapping.AmountColumn.Should().Be(3);
            mapping.DateFormat.Pattern.Should().Be("d/M/yyyy", "October 1–3, not January to March");
        });
    }

    [Fact]
    public void Table_without_a_header_names_its_columns()
    {
        var table = CsvTable.Parse("2026-10-01,Coffee,-3.50\n2026-10-02,Salary,1500.00\n");
        table.Headers.Should().Equal(Enumerable.Range(1, 3).Select(i => string.Format(AppResources.ImportColumnName, i)));
        table.Rows.Should().HaveCount(2);
    }

    [Fact]
    public void Latin1_files_are_read_too()
    {
        var bytes = Encoding.Latin1.GetBytes("Date,Description,Amount\n2026-10-01,Panadería,-3.50\n");
        var table = CsvTable.Read(new MemoryStream(bytes));
        table.Rows.Single()[1].Should().Be("Panadería");
    }

    [Fact]
    public void Utf8_with_bom_keeps_the_first_header_clean()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("Date,Amount\n2026-10-01,-3.50\n")).ToArray();
        CsvTable.Read(new MemoryStream(bytes)).Headers[0].Should().Be("Date");
    }

    // ---- Column guess ----

    [Fact]
    public void Guess_uses_spanish_header_names_not_the_balance()
    {
        var table = CsvTable.Parse("""
            Fecha valor;Fecha;Descripción;Saldo;Importe
            01/10/2026;30/09/2026;Compra Éxito;1.955.000,00;-45.000,00
            02/10/2026;02/10/2026;Nómina;3.955.000,00;2.000.000,00
            """);

        var mapping = StatementMapping.Guess(table)!;

        mapping.DateColumn.Should().Be(1, "an exact name beats one that contains it");
        mapping.DescriptionColumn.Should().Be(2);
        mapping.AmountColumn.Should().Be(4);
        mapping.DecimalSeparator.Should().Be(',');
        mapping.DateFormat.Pattern.Should().Be("d/M/yyyy");
    }

    [Fact]
    public void Guess_without_names_goes_by_the_values()
    {
        var table = CsvTable.Parse("2026-10-01,REF1,Coffee at the corner,-3.50\n2026-10-02,REF2,Salary October,1500.00\n");

        var mapping = StatementMapping.Guess(table)!;

        mapping.DateColumn.Should().Be(0);
        mapping.AmountColumn.Should().Be(3);
        mapping.DescriptionColumn.Should().Be(2, "the longest texts");
    }

    [Fact]
    public void Guess_fails_without_an_amount_column() =>
        StatementMapping.Guess(CsvTable.Parse("Date,Description\n2026-10-01,Coffee\n")).Should().BeNull();
}
