using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MoneyRecord.Services.Pdf
{
    /// <summary>
    /// Lays out a <see cref="PeriodReport"/> as a printable PDF (MigraDoc): title and period, totals per currency with the
    /// change against the period before, spending and income by category (a bar per share), budgets, balances at the end
    /// of the period and every entry. Texts follow the app language, dates and amounts the app culture; A4, or Letter
    /// where the region doesn't use metric paper.
    /// </summary>
    public static partial class PeriodReportPdf
    {
        private static readonly Color Primary = new(0x51, 0x2B, 0xD4);
        private static readonly Color Muted = new(0x66, 0x66, 0x66);
        private static readonly Color Rule = new(0xDD, 0xDD, 0xDD);
        private static readonly Color HeaderFill = new(0xF1, 0xED, 0xFC);
        private static readonly Color Positive = new(0x2E, 0x7D, 0x32);
        private static readonly Color Negative = new(0xC6, 0x28, 0x28);
        private static readonly Color Warning = new(0xE6, 0x51, 0x00);

        private static readonly Unit Margin = Unit.FromCentimeter(1.8);

        public static void Write(PeriodReport report, Stream output)
        {
            PdfFonts.Install();
            var renderer = new PdfDocumentRenderer { Document = Build(report) };
            renderer.RenderDocument();
            renderer.PdfDocument.Save(output, closeStream: false);
        }

        public static Document Build(PeriodReport report)
        {
            var document = new Document();
            document.Info.Title = $"{AppResources.ReportTitle} · {PeriodText(report.Request)}";
            document.Info.Author = AppResources.AppTitle;
            DefineStyles(document);

            var section = document.AddSection();
            var format = RegionInfo.CurrentRegion.IsMetric ? PageFormat.A4 : PageFormat.Letter;
            PageSetup.GetPageSize(format, out var pageWidth, out var pageHeight);
            section.PageSetup = document.DefaultPageSetup.Clone();
            section.PageSetup.PageWidth = pageWidth;
            section.PageSetup.PageHeight = pageHeight;
            section.PageSetup.TopMargin = section.PageSetup.BottomMargin = Margin;
            section.PageSetup.LeftMargin = section.PageSetup.RightMargin = Margin;
            section.PageSetup.FooterDistance = Unit.FromCentimeter(0.8);
            var width = pageWidth - Margin * 2;

            AddFooter(section);
            AddTitle(section, report);
            AddSummary(section, report, width);
            AddCategories(section, AppResources.ReportSpendingByCategory, AppResources.NoExpenseData, report.Expenses, report, width);
            AddCategories(section, AppResources.ReportIncomeByCategory, AppResources.NoIncomeData, report.Income, report, width);
            AddBudgets(section, report, width);
            AddBalances(section, report, width);
            AddEntries(section, report, width);
            MakePrintable(section);
            return document;
        }

        /// <summary>Every text run through <see cref="PdfFonts.Printable"/>: names and amounts can hold what Open Sans lacks.</summary>
        private static void MakePrintable(DocumentObject? item)
        {
            switch (item)
            {
                case Text text:
                    text.Content = PdfFonts.Printable(text.Content);
                    break;
                case Paragraph paragraph:
                    foreach (DocumentObject? element in paragraph.Elements)
                        MakePrintable(element);
                    break;
                case FormattedText formatted:
                    foreach (DocumentObject? element in formatted.Elements)
                        MakePrintable(element);
                    break;
                case Table table:
                    foreach (Row row in table.Rows)
                        foreach (Cell cell in row.Cells)
                            foreach (DocumentObject? element in cell.Elements)
                                MakePrintable(element);
                    break;
                case Section section:
                    foreach (DocumentObject? element in section.Elements)
                        MakePrintable(element);
                    foreach (DocumentObject? element in section.Footers.Primary.Elements)
                        MakePrintable(element);
                    break;
            }
        }

        /// <summary>"Oct 1, 2026 – Oct 31, 2026", or one date for a single day.</summary>
        public static string PeriodText(ReportRequest request)
        {
            var start = DateFormatter.Format(request.Start, DateStyle.Medium);
            var end = DateFormatter.Format(request.End, DateStyle.Medium);
            return request.Start.Date == request.End.Date ? start : $"{start} – {end}";
        }

        private static void DefineStyles(Document document)
        {
            var normal = document.Styles[StyleNames.Normal]!;
            normal.Font.Name = PdfFonts.Family;
            normal.Font.Size = 9;

            var heading = document.Styles[StyleNames.Heading1]!;
            heading.Font.Size = 13;
            heading.Font.Bold = true;
            heading.Font.Color = Primary;
            heading.ParagraphFormat.SpaceBefore = Unit.FromPoint(16);
            heading.ParagraphFormat.SpaceAfter = Unit.FromPoint(6);
            heading.ParagraphFormat.KeepWithNext = true;

            var note = document.Styles.AddStyle("Note", StyleNames.Normal);
            note.Font.Size = 8;
            note.Font.Color = Muted;
            note.ParagraphFormat.SpaceBefore = Unit.FromPoint(3);
        }

        private static void AddFooter(Section section)
        {
            var footer = section.Footers.Primary.AddParagraph();
            footer.Style = "Note";
            footer.Format.Alignment = ParagraphAlignment.Center;
            // "Page {0} of {1}": the fields go where the language puts the placeholders
            foreach (var part in PlaceholderRegex().Split(AppResources.ReportPageOf))
            {
                if (part == "{0}")
                    footer.AddPageField();
                else if (part == "{1}")
                    footer.AddNumPagesField();
                else if (part.Length > 0)
                    footer.AddText(part);
            }
        }

        private static void AddTitle(Section section, PeriodReport report)
        {
            var title = section.AddParagraph(AppResources.ReportTitle);
            title.Format.Font.Size = 20;
            title.Format.Font.Bold = true;
            title.Format.Font.Color = Primary;

            var period = section.AddParagraph(PeriodText(report.Request));
            period.Format.Font.Size = 12;
            period.Format.SpaceBefore = Unit.FromPoint(2);

            var created = section.AddParagraph(string.Format(AppResources.ReportCreatedOn,
                $"{DateFormatter.Format(report.CreatedAt, DateStyle.Medium)} {report.CreatedAt.ToString("t", CultureInfo.CurrentCulture)}"));
            created.Style = "Note";
        }

        private static void AddSummary(Section section, PeriodReport report, Unit width)
        {
            section.AddParagraph(AppResources.Summary, StyleNames.Heading1);
            var table = NewTable(section, width, (0.16, false), (0.28, true), (0.28, true), (0.28, true));
            Header(table, AppResources.Currency, AppResources.Income, AppResources.Expenses, AppResources.NetIncome);

            foreach (var totals in report.TotalsRows)
            {
                var previous = report.PreviousOf(totals);
                var row = table.AddRow();
                row.Cells[0].AddParagraph(totals.CurrencyCode);
                AmountWithDelta(row.Cells[1], totals.ApproximateMark + totals.IncomeText, new PeriodDelta(totals.Income, previous.Income));
                AmountWithDelta(row.Cells[2], totals.ApproximateMark + totals.ExpenseText, new PeriodDelta(totals.Expense, previous.Expense));
                var net = row.Cells[3].AddParagraph(totals.ApproximateMark + totals.NetText);
                net.Format.Font.Bold = true;
                net.Format.Font.Color = totals.IsNetNegative ? Negative : Positive;
            }

            if (report.Totals.IsConverted)
                section.AddParagraph(string.Format(AppResources.ReportConvertedTo, report.Totals.Base.CurrencyCode), "Note");
        }

        private static void AmountWithDelta(Cell cell, string amount, PeriodDelta delta)
        {
            cell.AddParagraph(amount);
            if (delta.Text.Length > 0)
                cell.AddParagraph(delta.Text).Style = "Note";
        }

        private static void AddCategories(Section section, string heading, string empty, ReportCategories categories,
            PeriodReport report, Unit width)
        {
            section.AddParagraph(heading, StyleNames.Heading1);
            if (categories.Report.Total == 0)
                section.AddParagraph(empty, "Note");
            else
                AddCategoryTable(section, categories, width);

            // Amounts in one currency: say which, and what was left out
            var severalCurrencies = report.Totals.Others.Count > 0 || report.Totals.IsConverted;
            if (severalCurrencies)
            {
                section.AddParagraph(string.Format(report.Request.Converted ? AppResources.ReportConvertedTo : AppResources.ReportInCurrencyOnly,
                    categories.CurrencyCode), "Note");
            }

            if (categories.NotConvertedText.Length > 0)
                section.AddParagraph(categories.NotConvertedText, "Note");
        }

        private static void AddCategoryTable(Section section, ReportCategories categories, Unit width)
        {
            var table = NewTable(section, width, (0.34, false), (0.36, false), (0.12, true), (0.18, true));
            Header(table, AppResources.Category, string.Empty, AppResources.ReportShare, AppResources.Amount);
            var barWidth = width * 0.36 - Unit.FromPoint(8);
            foreach (var category in categories.Report.Categories)
            {
                var row = table.AddRow();
                row.Cells[0].AddParagraph(category.CategoryName);
                // The share as a bar: a shaded paragraph whose right indent leaves the rest of the column empty
                var bar = row.Cells[1].AddParagraph("\u00A0");
                bar.Format.Shading.Color = Primary;
                bar.Format.Font.Size = 6;
                bar.Format.RightIndent = barWidth * (1 - Math.Clamp(category.Share, 0.005, 1));
                row.Cells[2].AddParagraph(category.Share.ToString("P1", CultureInfo.CurrentCulture));
                row.Cells[3].AddParagraph(categories.FormatAmount(category.Amount));
            }

            var total = table.AddRow();
            total.Format.Font.Bold = true;
            total.Cells[0].AddParagraph(AppResources.Total);
            total.Cells[3].AddParagraph(categories.FormatAmount(categories.Report.Total));
        }

        private static void AddBudgets(Section section, PeriodReport report, Unit width)
        {
            if (report.Budgets.Count == 0)
                return;

            section.AddParagraph(AppResources.Budgets, StyleNames.Heading1);
            var table = NewTable(section, width, (0.34, false), (0.22, true), (0.22, true), (0.22, true));
            Header(table, AppResources.Category, AppResources.TotalSpent, AppResources.ReportLimit, AppResources.ReportStatus);
            foreach (var budget in report.Budgets)
            {
                var row = table.AddRow();
                row.Cells[0].AddParagraph(budget.CategoryName);
                if (budget.HasUncounted)
                    row.Cells[0].AddParagraph(budget.UncountedText).Style = "Note";
                row.Cells[1].AddParagraph(MoneyFormatter.Format(budget.SpentAmount));
                row.Cells[2].AddParagraph(MoneyFormatter.Format(budget.LimitAmount));
                var status = row.Cells[3].AddParagraph(budget.StatusText);
                status.Format.Font.Color = budget.Level switch
                {
                    BudgetLevel.Exceeded => Negative,
                    BudgetLevel.Warning or BudgetLevel.High => Warning,
                    _ => Positive
                };
            }
        }

        private static void AddBalances(Section section, PeriodReport report, Unit width)
        {
            section.AddParagraph(AppResources.Accounts, StyleNames.Heading1);
            var table = NewTable(section, width, (0.5, false), (0.16, false), (0.34, true));
            Header(table, AppResources.Account, AppResources.Currency,
                string.Format(AppResources.ReportBalanceOn, DateFormatter.Format(report.Request.End, DateStyle.Medium)));
            foreach (var balance in report.Balances)
            {
                var row = table.AddRow();
                row.Cells[0].AddParagraph(balance.AccountName);
                row.Cells[1].AddParagraph(MoneyFormatter.ResolveCurrencyCode(balance.CurrencyCode));
                Amount(row.Cells[2], balance.CurrentBalance, balance.CurrencyCode, signed: false);
            }

            foreach (var total in report.BalanceTotals)
            {
                var row = table.AddRow();
                row.Format.Font.Bold = true;
                row.Cells[0].AddParagraph(AppResources.Total);
                row.Cells[1].AddParagraph(total.CurrencyCode);
                Amount(row.Cells[2], total.Amount, total.CurrencyCode, signed: false);
            }
        }

        private static void AddEntries(Section section, PeriodReport report, Unit width)
        {
            section.AddParagraph(AppResources.Transactions, StyleNames.Heading1);
            if (report.Entries.Count == 0)
            {
                section.AddParagraph(AppResources.NoTransactionsInPeriod, "Note");
                return;
            }

            // The year only when the period spans more than one
            var dateStyle = report.Request.Start.Year == report.Request.End.Year ? DateStyle.MonthDay : DateStyle.Medium;
            var table = NewTable(section, width, (dateStyle == DateStyle.MonthDay ? 0.1 : 0.15, false), (0.32, false),
                (0.17, false), (0.21, false), (dateStyle == DateStyle.MonthDay ? 0.2 : 0.15, true));
            table.Format.Font.Size = 8;
            Header(table, AppResources.Date, AppResources.Description, AppResources.Category, AppResources.Account, AppResources.Amount);
            foreach (var entry in report.Entries)
            {
                var row = table.AddRow();
                row.Cells[0].AddParagraph(DateFormatter.Format(entry.Date, dateStyle));
                row.Cells[1].AddParagraph(entry.Description);
                if (entry.Payee.Length > 0 && entry.Payee != entry.Description)
                    row.Cells[1].AddParagraph(entry.Payee).Style = "Note";
                row.Cells[2].AddParagraph(entry.Category);
                row.Cells[3].AddParagraph(entry.Account);
                Amount(row.Cells[4], entry.Amount, entry.CurrencyCode, signed: !entry.IsTransfer);
            }
        }

        private static void Amount(Cell cell, decimal amount, string currencyCode, bool signed)
        {
            var paragraph = cell.AddParagraph(signed
                ? MoneyFormatter.FormatSigned(amount, currencyCode)
                : MoneyFormatter.Format(amount, currencyCode));
            if (signed && amount != 0)
                paragraph.Format.Font.Color = amount > 0 ? Positive : Negative;
            else if (!signed && amount < 0)
                paragraph.Format.Font.Color = Negative;
        }

        /// <summary>A full-width table: each column a share of the width, right-aligned when it holds amounts.</summary>
        private static Table NewTable(Section section, Unit width, params (double Share, bool Right)[] columns)
        {
            var table = section.AddTable();
            table.Borders.Bottom.Width = 0;
            table.LeftPadding = table.RightPadding = Unit.FromPoint(4);
            table.TopPadding = table.BottomPadding = Unit.FromPoint(3);
            foreach (var (share, right) in columns)
            {
                var column = table.AddColumn(width * share);
                column.Format.Alignment = right ? ParagraphAlignment.Right : ParagraphAlignment.Left;
            }

            return table;
        }

        /// <summary>The header row, repeated on every page the table runs onto; rows below get a hairline between them.</summary>
        private static void Header(Table table, params string[] titles)
        {
            var row = table.AddRow();
            row.HeadingFormat = true;
            row.Shading.Color = HeaderFill;
            row.Format.Font.Bold = true;
            for (var i = 0; i < titles.Length; i++)
                row.Cells[i].AddParagraph(titles[i]);
            table.Borders.Color = Rule;
            table.Borders.Bottom.Width = Unit.FromPoint(0.5);
            table.Borders.Top.Width = 0;
            table.Borders.Left.Width = table.Borders.Right.Width = 0;
        }

        [GeneratedRegex(@"(\{[01]\})")]
        private static partial Regex PlaceholderRegex();
    }
}
