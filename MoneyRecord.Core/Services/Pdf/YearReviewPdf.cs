using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using MoneyRecord.Helpers;
using MoneyRecord.Resources.Strings;
using System.Globalization;

namespace MoneyRecord.Services.Pdf
{
    /// <summary>
    /// The year in review as a one-page PDF: a title, then each <see cref="YearReviewSection"/> as a heading over its
    /// label / value lines. Fonts, paper and language as in <see cref="PeriodReportPdf"/>.
    /// </summary>
    public static class YearReviewPdf
    {
        private static readonly Color Primary = new(0x51, 0x2B, 0xD4);
        private static readonly Color Muted = new(0x66, 0x66, 0x66);
        private static readonly Color Rule = new(0xDD, 0xDD, 0xDD);

        private static readonly Unit Margin = Unit.FromCentimeter(2);

        public static void Write(YearReview review, bool includeAmounts, Stream output)
        {
            PdfFonts.Install();
            var renderer = new PdfDocumentRenderer { Document = Build(review, includeAmounts) };
            renderer.RenderDocument();
            renderer.PdfDocument.Save(output, closeStream: false);
        }

        public static Document Build(YearReview review, bool includeAmounts)
        {
            var title = string.Format(AppResources.YearReviewTitle, review.Year);
            var document = new Document();
            document.Info.Title = PdfFonts.Printable(title);
            document.Info.Author = AppResources.AppTitle;

            var normal = document.Styles[StyleNames.Normal]!;
            normal.Font.Name = PdfFonts.Family;
            normal.Font.Size = 11;
            var heading = document.Styles[StyleNames.Heading1]!;
            heading.Font.Size = 14;
            heading.Font.Bold = true;
            heading.Font.Color = Primary;
            heading.ParagraphFormat.SpaceBefore = Unit.FromPoint(18);
            heading.ParagraphFormat.SpaceAfter = Unit.FromPoint(6);
            heading.ParagraphFormat.KeepWithNext = true;

            var section = document.AddSection();
            var format = RegionInfo.CurrentRegion.IsMetric ? PageFormat.A4 : PageFormat.Letter;
            PageSetup.GetPageSize(format, out var pageWidth, out var pageHeight);
            section.PageSetup = document.DefaultPageSetup.Clone();
            section.PageSetup.PageWidth = pageWidth;
            section.PageSetup.PageHeight = pageHeight;
            section.PageSetup.TopMargin = section.PageSetup.BottomMargin = Margin;
            section.PageSetup.LeftMargin = section.PageSetup.RightMargin = Margin;
            var width = pageWidth - Margin * 2;

            var heard = section.AddParagraph(PdfFonts.Printable(title));
            heard.Format.Font.Size = 24;
            heard.Format.Font.Bold = true;
            heard.Format.Font.Color = Primary;

            if (review.IsApproximate && includeAmounts)
            {
                var note = section.AddParagraph(PdfFonts.Printable(string.Format(AppResources.ReportConvertedTo, review.CurrencyCode)));
                note.Format.Font.Size = 9;
                note.Format.Font.Color = Muted;
            }

            foreach (var part in YearReviewSections.Build(review, includeAmounts))
            {
                section.AddParagraph(PdfFonts.Printable(part.Title), StyleNames.Heading1);
                var table = section.AddTable();
                table.Borders.Color = Rule;
                table.Borders.Bottom.Width = Unit.FromPoint(0.5);
                table.Borders.Top.Width = table.Borders.Left.Width = table.Borders.Right.Width = 0;
                table.TopPadding = table.BottomPadding = Unit.FromPoint(4);
                table.LeftPadding = table.RightPadding = Unit.FromPoint(2);
                table.AddColumn(width * 0.62).Format.Alignment = ParagraphAlignment.Left;
                table.AddColumn(width * 0.38).Format.Alignment = ParagraphAlignment.Right;
                foreach (var line in part.Lines)
                {
                    var row = table.AddRow();
                    row.Cells[0].AddParagraph(PdfFonts.Printable(line.Label));
                    row.Cells[1].AddParagraph(PdfFonts.Printable(line.Value)).Format.Font.Bold = true;
                }
            }

            return document;
        }
    }
}
