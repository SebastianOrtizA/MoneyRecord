using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using System.Text;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public sealed class OfxQifParserTests
{
    // OFX 1.x: SGML header, values end at the line end, STMTTRN never closed, CRLF
    private const string Sgml =
        "OFXHEADER:100\r\nDATA:OFXSGML\r\nVERSION:102\r\nENCODING:USASCII\r\nCHARSET:1252\r\n\r\n" +
        "<OFX><SIGNONMSGSRSV1><SONRS><STATUS><CODE>0<SEVERITY>INFO</STATUS><DTSERVER>20261014</SONRS></SIGNONMSGSRSV1>\r\n" +
        "<BANKMSGSRSV1><STMTTRNRS><STMTRS><CURDEF>EUR\r\n<BANKACCTFROM><BANKID>1<ACCTID>2<ACCTTYPE>CHECKING</BANKACCTFROM>\r\n" +
        "<BANKTRANLIST><DTSTART>20261001<DTEND>20261014\r\n" +
        "<STMTTRN><TRNTYPE>DEBIT<DTPOSTED>20261003120000.000[-5:EST]<TRNAMT>-45.20<FITID>A1<NAME>CAFÉ PARIS<MEMO>card 1234\r\n" +
        "<STMTTRN><TRNTYPE>CREDIT<DTPOSTED>20261005<TRNAMT>1500.00<FITID>A2<NAME>Salary &amp; bonus\r\n" +
        "</BANKTRANLIST><LEDGERBAL><BALAMT>100.00<DTASOF>20261014</LEDGERBAL></STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>";

    private const string Xml =
        "<?xml version=\"1.0\"?><?OFX OFXHEADER=\"200\"?><OFX><CREDITCARDMSGSRSV1><CCSTMTTRNRS><CCSTMTRS>\n" +
        "  <CURDEF>USD</CURDEF>\n  <BANKTRANLIST>\n" +
        "    <STMTTRN>\n      <TRNTYPE>DEBIT</TRNTYPE>\n      <DTPOSTED>20261101</DTPOSTED>\n      <TRNAMT>-12,50</TRNAMT>\n" +
        "      <FITID>X9</FITID>\n      <NAME>Shop</NAME>\n      <MEMO>Shop</MEMO>\n    </STMTTRN>\n" +
        "    <STMTTRN>\n      <DTPOSTED>bad</DTPOSTED>\n      <TRNAMT>3.00</TRNAMT>\n    </STMTTRN>\n" +
        "  </BANKTRANLIST>\n</CCSTMTRS></CCSTMTTRNRS></CREDITCARDMSGSRSV1></OFX>";

    [Fact]
    public void Ofx_sgml_without_closing_tags_is_read()
    {
        var statement = OfxParser.Parse(Sgml);

        statement.CurrencyCode.Should().Be("EUR");
        statement.Entries.Should().Equal(
            new StatementEntry(1, new DateTime(2026, 10, 3), "CAFÉ PARIS - card 1234", -45.20m, "A1"),
            new StatementEntry(2, new DateTime(2026, 10, 5), "Salary & bonus", 1500m, "A2"));
    }

    [Fact]
    public void Ofx_xml_is_read_with_a_decimal_comma_and_unreadable_dates_stay_null()
    {
        var statement = OfxParser.Parse(Xml);

        statement.CurrencyCode.Should().Be("USD");
        statement.Entries.Should().Equal(
            new StatementEntry(1, new DateTime(2026, 11, 1), "Shop", -12.50m, "X9"),
            new StatementEntry(2, null, "", 3m));
    }

    [Fact]
    public void Ofx_in_latin1_is_decoded_when_it_is_not_utf8()
    {
        var bytes = Encoding.Latin1.GetBytes(Sgml.Replace("\r\n", "\n"));

        OfxParser.Read(new MemoryStream(bytes)).Entries[0].Description.Should().StartWith("CAFÉ");
    }

    [Fact]
    public void A_file_without_movements_is_empty()
    {
        OfxParser.Parse("<OFX><BANKTRANLIST></BANKTRANLIST></OFX>").Entries.Should().BeEmpty();
    }

    private const string Qif =
        "!Account\nNChecking\nTBank\n^\n!Type:Bank\nD5/ 4'26\nT-1,234.50\nPCoffee Shop\nMLatte\n^\n" +
        "D6/4/2026\nT20.00\nPRefund\n^\n!Type:Cat\nNFood\n^\n!Type:Bank\nD13/4/2026\nT-3.00\nPTaxi\nSA\n$-3.00\n^\n";

    [Fact]
    public void Qif_offers_both_date_orders_when_the_dates_allow_it_and_reads_with_the_chosen_one()
    {
        var qif = QifParser.Parse(Qif.Replace("D13/4/2026", "D12/4/2026"));
        qif.Count.Should().Be(3);

        var formats = qif.DateFormats;
        formats.Select(f => f.Pattern).Should().Contain(["d/M/yyyy", "M/d/yyyy"]);

        var dayFirst = qif.ToStatement(formats.First(f => f.Pattern == "d/M/yyyy")).Entries;
        dayFirst.Select(e => e.Date).Should().Equal(new DateTime(2026, 4, 5), new DateTime(2026, 4, 6), new DateTime(2026, 4, 12));
        var monthFirst = qif.ToStatement(formats.First(f => f.Pattern == "M/d/yyyy")).Entries;
        monthFirst[0].Date.Should().Be(new DateTime(2026, 5, 4));
    }

    [Fact]
    public void Qif_day_over_twelve_decides_the_order_and_amounts_and_text_are_read()
    {
        var qif = QifParser.Parse(Qif);

        // 13/4 only fits day-first: it reads every date, so it comes first
        var formats = qif.DateFormats;
        formats[0].Pattern.Should().Be("d/M/yyyy");
        var entries = qif.ToStatement(formats[0]).Entries;

        entries.Select(e => (e.Amount, e.Description)).Should().Equal(
            (-1234.50m, "Coffee Shop - Latte"), (20m, "Refund"), (-3m, "Taxi"));
        entries.Should().OnlyContain(e => e.ExternalId == null);
    }

    [Fact]
    public void Qif_decimal_commas_are_read()
    {
        var qif = QifParser.Parse("!Type:CCard\nD31.12.2026\nT-1.234,50\nPShop\n^\nD01.12.2026\nT-0,99\nPKiosk\n^\n");

        qif.ToStatement(qif.DateFormats[0]).Entries.Select(e => e.Amount).Should().Equal(-1234.50m, -0.99m);
    }
}
