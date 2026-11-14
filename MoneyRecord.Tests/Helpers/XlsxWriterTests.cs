using System.IO.Compression;
using System.Xml.Linq;
using AwesomeAssertions;
using MoneyRecord.Helpers;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class XlsxWriterTests
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static ZipArchive Write(params XlsxSheet[] sheets)
    {
        var stream = new MemoryStream();
        XlsxWriter.Write(stream, sheets);
        stream.Position = 0;
        return new ZipArchive(stream, ZipArchiveMode.Read);
    }

    private static XDocument Part(ZipArchive zip, string name)
    {
        using var reader = zip.GetEntry(name)!.Open();
        return XDocument.Load(reader);
    }

    [Fact]
    public void Every_part_is_well_formed_xml_and_each_sheet_is_listed()
    {
        using var zip = Write(new XlsxSheet("One", ["A"], []), new XlsxSheet("Two", ["B"], []));

        foreach (var name in new[] { "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml", "xl/_rels/workbook.xml.rels", "xl/styles.xml", "xl/worksheets/sheet1.xml", "xl/worksheets/sheet2.xml" })
            Part(zip, name);
        Part(zip, "xl/workbook.xml").Descendants(Ns + "sheet").Select(s => (string?)s.Attribute("name")).Should().Equal("One", "Two");
    }

    [Fact]
    public void Dates_and_amounts_are_typed_cells_and_text_is_kept()
    {
        using var zip = Write(new XlsxSheet("Data", ["Date", "Amount", "Count", "Text"],
            [[new DateTime(2026, 10, 3, 18, 0, 0), 12.5m, 3, "Café & <co>"]]));

        var cells = Part(zip, "xl/worksheets/sheet1.xml").Descendants(Ns + "row").Last().Elements(Ns + "c").ToList();

        cells[0].Element(Ns + "v")!.Value.Should().Be(new DateTime(2026, 10, 3).ToOADate().ToString("0"));
        cells[0].Attribute("s")!.Value.Should().Be("2");
        cells[1].Element(Ns + "v")!.Value.Should().Be("12.5");
        cells[2].Attribute("t").Should().BeNull();
        cells[3].Element(Ns + "is")!.Value.Should().Be("Café & <co>");
    }

    [Theory]
    [InlineData("=SUM(A1:A2)")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("@cmd")]
    public void Text_starting_with_a_formula_character_stays_a_text_cell(string text)
    {
        using var zip = Write(new XlsxSheet("Data", ["Text"], [[text]]));

        var cell = Part(zip, "xl/worksheets/sheet1.xml").Descendants(Ns + "c").Last();

        cell.Attribute("t")!.Value.Should().Be("inlineStr");
        cell.Element(Ns + "f").Should().BeNull();
        cell.Element(Ns + "is")!.Value.Should().Be(text);
    }

    [Fact]
    public void Control_characters_and_bad_sheet_names_are_cleaned()
    {
        using var zip = Write(new XlsxSheet("A/B:C*[x]?" + new string('z', 40), ["H"], [["a\u0001b"]]));

        var name = (string)Part(zip, "xl/workbook.xml").Descendants(Ns + "sheet").Single().Attribute("name")!;

        name.Should().HaveLength(31).And.NotContainAny("/", ":", "*", "[", "]", "?");
        Part(zip, "xl/worksheets/sheet1.xml").Descendants(Ns + "is").Last().Value.Should().Be("ab");
    }

    [Theory]
    [InlineData(0, "A")]
    [InlineData(25, "Z")]
    [InlineData(26, "AA")]
    [InlineData(27, "AB")]
    [InlineData(701, "ZZ")]
    [InlineData(702, "AAA")]
    public void Column_letters(int index, string expected) => XlsxWriter.Column(index).Should().Be(expected);
}
