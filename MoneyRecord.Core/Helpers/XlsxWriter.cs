using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace MoneyRecord.Helpers
{
    /// <summary>One worksheet: a header row and the rows below it. Cells are <see cref="string"/>, <see cref="DateTime"/> (date), <see cref="decimal"/>, <see cref="int"/>, <see cref="long"/> or null (empty).</summary>
    public sealed record XlsxSheet(string Name, IReadOnlyList<string> Header, IReadOnlyList<object?[]> Rows);

    /// <summary>
    /// Writes a minimal .xlsx (SpreadsheetML in a zip) that Excel, LibreOffice and Google Sheets open: typed number and date
    /// cells, a bold header row. Text is always written as a text cell, never a formula, so a description starting with
    /// "=", "+", "-" or "@" stays text.
    /// </summary>
    public static class XlsxWriter
    {
        private const string Xml = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>";
        private const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private const string PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

        // Style indexes in styles.xml: 0 default, 1 bold header, 2 date, 3 two-decimal number
        private const int HeaderStyle = 1, DateStyle = 2, MoneyStyle = 3;

        public static void Write(Stream output, IReadOnlyList<XlsxSheet> sheets)
        {
            using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);

            var types = new StringBuilder(Xml + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
            var workbook = new StringBuilder($"{Xml}<workbook xmlns=\"{MainNs}\" xmlns:r=\"{RelNs}\"><sheets>");
            var workbookRels = new StringBuilder($"{Xml}<Relationships xmlns=\"{PackageRelNs}\">");

            for (var i = 0; i < sheets.Count; i++)
            {
                var n = i + 1;
                types.Append($"<Override PartName=\"/xl/worksheets/sheet{n}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
                workbook.Append($"<sheet name=\"{Escape(SheetName(sheets[i].Name))}\" sheetId=\"{n}\" r:id=\"rId{n}\"/>");
                workbookRels.Append($"<Relationship Id=\"rId{n}\" Type=\"{RelNs}/worksheet\" Target=\"worksheets/sheet{n}.xml\"/>");
                AddEntry(zip, $"xl/worksheets/sheet{n}.xml", SheetXml(sheets[i]));
            }

            types.Append("</Types>");
            workbook.Append("</sheets></workbook>");
            workbookRels.Append($"<Relationship Id=\"rId{sheets.Count + 1}\" Type=\"{RelNs}/styles\" Target=\"styles.xml\"/></Relationships>");

            AddEntry(zip, "[Content_Types].xml", types.ToString());
            AddEntry(zip, "_rels/.rels", $"{Xml}<Relationships xmlns=\"{PackageRelNs}\"><Relationship Id=\"rId1\" Type=\"{RelNs}/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            AddEntry(zip, "xl/workbook.xml", workbook.ToString());
            AddEntry(zip, "xl/_rels/workbook.xml.rels", workbookRels.ToString());
            AddEntry(zip, "xl/styles.xml", $"{Xml}<styleSheet xmlns=\"{MainNs}\"><fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts><fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills><borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs><cellXfs count=\"4\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/><xf numFmtId=\"14\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/><xf numFmtId=\"2\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/></cellXfs></styleSheet>");
        }

        private static string SheetXml(XlsxSheet sheet)
        {
            var sb = new StringBuilder($"{Xml}<worksheet xmlns=\"{MainNs}\"><sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews><sheetData>");
            AppendRow(sb, 1, sheet.Header.Select(h => (object?)h).ToArray(), HeaderStyle);
            for (var r = 0; r < sheet.Rows.Count; r++)
                AppendRow(sb, r + 2, sheet.Rows[r], null);
            return sb.Append("</sheetData></worksheet>").ToString();
        }

        private static void AppendRow(StringBuilder sb, int row, object?[] cells, int? textStyle)
        {
            sb.Append($"<row r=\"{row}\">");
            for (var c = 0; c < cells.Length; c++)
            {
                var reference = $"{Column(c)}{row}";
                switch (cells[c])
                {
                    case null:
                        break;
                    case DateTime date:
                        sb.Append($"<c r=\"{reference}\" s=\"{DateStyle}\"><v>{date.Date.ToOADate().ToString("0", CultureInfo.InvariantCulture)}</v></c>");
                        break;
                    case decimal money:
                        sb.Append($"<c r=\"{reference}\" s=\"{MoneyStyle}\"><v>{money.ToString(CultureInfo.InvariantCulture)}</v></c>");
                        break;
                    case int or long:
                        sb.Append($"<c r=\"{reference}\"><v>{Convert.ToString(cells[c], CultureInfo.InvariantCulture)}</v></c>");
                        break;
                    default:
                        var text = Clean(Convert.ToString(cells[c], CultureInfo.InvariantCulture) ?? string.Empty);
                        if (text.Length == 0) break;
                        var style = textStyle is int s ? $" s=\"{s}\"" : string.Empty;
                        sb.Append($"<c r=\"{reference}\" t=\"inlineStr\"{style}><is><t xml:space=\"preserve\">{Escape(text)}</t></is></c>");
                        break;
                }
            }
            sb.Append("</row>");
        }

        /// <summary>A, B, ... Z, AA, AB, ...</summary>
        public static string Column(int index)
        {
            var name = string.Empty;
            for (var n = index + 1; n > 0; n = (n - 1) / 26)
                name = (char)('A' + (n - 1) % 26) + name;
            return name;
        }

        // Excel rejects names over 31 characters or with : \ / ? * [ ]
        private static string SheetName(string name)
        {
            var clean = new string(name.Where(c => !":\\/?*[]".Contains(c)).ToArray()).Trim();
            if (clean.Length == 0) clean = "Sheet";
            return clean.Length > 31 ? clean[..31] : clean;
        }

        // XML 1.0 can't hold most control characters
        private static string Clean(string text) =>
            new(text.Where(c => c >= ' ' || c is '\t' or '\n' or '\r').ToArray());

        private static string Escape(string text) => SecurityElement.Escape(text) ?? string.Empty;

        private static void AddEntry(ZipArchive zip, string name, string xml)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using var stream = entry.Open();
            stream.Write(new UTF8Encoding(false).GetBytes(xml));
        }
    }
}
