using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Pdf;
using NSubstitute;
using PdfSharp.Pdf.IO;
using System.Globalization;
using Xunit;

namespace MoneyRecord.Tests.Services;

public sealed class ReportExportServiceTests : IDisposable
{
    private static readonly DateTime Start = new(2026, 9, 1);
    private static readonly DateTime End = new DateTime(2026, 10, 1).AddTicks(-1);

    private readonly IConversionService _conversion = Substitute.For<IConversionService>();
    private readonly ITransactionEnrichmentService _enrichment = Substitute.For<ITransactionEnrichmentService>();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly IBudgetSummaryService _budgets = Substitute.For<IBudgetSummaryService>();
    private readonly IBalanceService _balances = Substitute.For<IBalanceService>();
    private readonly ReportExportService _sut;
    private readonly string _pdf = Path.Combine(Path.GetTempPath(), $"moneyrecord-report-{Guid.NewGuid():N}.pdf");

    public ReportExportServiceTests()
    {
        _sut = new ReportExportService(_conversion, _enrichment, _categories, _accounts, _budgets, _balances);
        var totals = new PeriodTotalsResult(new PeriodTotals("USD", 1000m, 250m), [new PeriodTotals("EUR", 0m, 40m)], IsConverted: false);
        _conversion.GetPeriodTotalsAsync(default, default).ReturnsForAnyArgs(totals);
        _conversion.ForReportAsync(default!, default!, default, default!).ReturnsForAnyArgs(call =>
            new ReportTransactions(call.Arg<IEnumerable<Transaction>>().ToList(), call.ArgAt<string>(3), [], IsApproximate: false));
        _categories.GetByTypeAsync(default).ReturnsForAnyArgs(new List<Category>());
        _accounts.GetAllAsync().Returns(new List<Account>());
        _budgets.GetSummaryAsync(default).ReturnsForAnyArgs(new List<BudgetProgress>());
        _balances.GetAllAccountBalancesAsync().Returns(new List<AccountBalanceInfo>());
        _balances.GetChangesSinceAsync(default).ReturnsForAnyArgs(new Dictionary<int, decimal>());
        _enrichment.GetEnrichedTransactionsAsync(default, default).ReturnsForAnyArgs(new List<Transaction>());
        _enrichment.GetEnrichedTransfersAsync(default, default).ReturnsForAnyArgs(new List<Transfer>());
    }

    public void Dispose() => File.Delete(_pdf);

    private static ReportRequest Request(bool converted = false) => new(Start, End, PeriodType.LastMonth, "USD", converted);

    [Fact]
    public async Task Balances_are_the_ones_at_the_end_of_the_period_without_the_debts_hidden_accounts()
    {
        _balances.GetAllAccountBalancesAsync().Returns(new List<AccountBalanceInfo>
        {
            new() { AccountId = 1, AccountName = "Wallet", CurrentBalance = 100m, CurrencyCode = "USD" },
            new() { AccountId = 2, AccountName = "bank", CurrentBalance = 500m, CurrencyCode = "USD" },
            new() { AccountId = 3, AccountName = "Ana", CurrentBalance = 300m, IsDebt = true }
        });
        // Since the day after the end: 20 spent from the wallet in October
        _balances.GetChangesSinceAsync(new DateTime(2026, 10, 1)).Returns(new Dictionary<int, decimal> { [1] = -20m });

        var report = await _sut.BuildAsync(Request());

        report.Balances.Select(b => (b.AccountName, b.CurrentBalance)).Should().Equal(("bank", 500m), ("Wallet", 120m));
        report.BalanceTotals.Should().Equal(new CurrencyAmount("USD", 620m));
    }

    [Fact]
    public async Task Entries_are_signed_transactions_and_transfers_oldest_first()
    {
        _enrichment.GetEnrichedTransactionsAsync(Start, End).Returns(new List<Transaction>
        {
            new() { Date = new DateTime(2026, 9, 20), Description = "", CategoryName = "Food", AccountName = "Wallet", Amount = 12.5m, Type = TransactionType.Expense, CurrencyCode = "USD", PayeeName = "Bakery" },
            new() { Date = new DateTime(2026, 9, 5), Description = "Pay", CategoryName = "Salary", AccountName = "Bank", Amount = 1000m, Type = TransactionType.Income, CurrencyCode = "USD" }
        });
        _enrichment.GetEnrichedTransfersAsync(Start, End).Returns(new List<Transfer>
        {
            new() { Date = new DateTime(2026, 9, 10), SourceAccountName = "Bank", DestinationAccountName = "Wallet", Amount = 50m, CurrencyCode = "USD" }
        });

        var entries = (await _sut.BuildAsync(Request())).Entries;

        entries.Select(e => (e.Description, e.Amount)).Should().Equal(("Pay", 1000m), (AppResources.ReportTransfer, 50m), ("Food", -12.5m));
        entries[1].Account.Should().Be("Bank → Wallet");
        entries[1].IsTransfer.Should().BeTrue();
        entries[2].Payee.Should().Be("Bakery");
    }

    [Fact]
    public async Task Category_shares_follow_the_reports_currency_choice()
    {
        _enrichment.GetEnrichedTransactionsAsync(Start, End).Returns(new List<Transaction>
        {
            new() { CategoryId = 1, CategoryName = "Food", Amount = 30m, Type = TransactionType.Expense },
            new() { CategoryId = 2, CategoryName = "Fun", Amount = 10m, Type = TransactionType.Expense },
            new() { CategoryId = 3, CategoryName = "Salary", Amount = 900m, Type = TransactionType.Income }
        });

        var report = await _sut.BuildAsync(new ReportRequest(Start, End, PeriodType.LastMonth, "EUR", Converted: false));

        await _conversion.Received(2).ForReportAsync(Arg.Any<IEnumerable<Transaction>>(), Arg.Any<IReadOnlyCollection<Account>>(), false, "EUR");
        report.Expenses.CurrencyCode.Should().Be("EUR");
        report.Expenses.Report.Categories.Select(c => (c.CategoryName, c.Share)).Should().Equal(("Food", 0.75), ("Fun", 0.25));
        report.Income.Report.Total.Should().Be(900m);
    }

    [Fact]
    public async Task Budgets_are_projected_onto_the_requested_period()
    {
        await _sut.BuildAsync(Request());

        await _budgets.Received(1).GetSummaryAsync(PeriodType.LastMonth, Start, End);
    }

    [Fact]
    public async Task The_previous_totals_are_matched_by_currency()
    {
        _conversion.GetPeriodTotalsAsync(new DateTime(2026, 8, 1), Arg.Any<DateTime>())
            .Returns(new PeriodTotalsResult(new PeriodTotals("USD", 800m, 200m), [], IsConverted: false));

        var report = await _sut.BuildAsync(Request());

        var rows = report.TotalsRows.ToList();
        report.PreviousOf(rows[0]).Expense.Should().Be(200m);
        report.PreviousOf(rows[1]).Should().Be(new PeriodTotals("EUR", 0m, 0m), "no euro spending the month before");
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("es-CO")]
    public async Task The_pdf_lays_out_every_entry_over_as_many_pages_as_needed(string culture)
    {
        _enrichment.GetEnrichedTransactionsAsync(Start, End).Returns(Enumerable.Range(0, 300)
            .Select(i => new Transaction
            {
                Date = Start.AddDays(i % 30), Description = $"Entry ñ € {i}", CategoryId = i % 5, CategoryName = $"Category {i % 5}",
                AccountName = "Wallet", Amount = i + 0.5m, Type = i % 7 == 0 ? TransactionType.Income : TransactionType.Expense, CurrencyCode = "USD"
            }).ToList());
        _budgets.GetSummaryAsync(default).ReturnsForAnyArgs(new List<BudgetProgress>
        {
            new() { CategoryName = "Category 1", LimitAmount = 100m, SpentAmount = 130m, Level = BudgetLevel.Exceeded }
        });
        _balances.GetAllAccountBalancesAsync().Returns(new List<AccountBalanceInfo>
        {
            new() { AccountId = 1, AccountName = "Wallet", CurrentBalance = -40m, CurrencyCode = "USD" }
        });

        var previous = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(culture);
        try
        {
            await _sut.WritePdfAsync(await _sut.BuildAsync(Request()), _pdf);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previousUi;
        }

        using var document = PdfReader.Open(_pdf, PdfDocumentOpenMode.Import);
        document.PageCount.Should().BeGreaterThan(3, "300 entries don't fit on a few pages");
    }

    [Fact]
    public async Task An_empty_period_still_makes_a_one_page_report()
    {
        _conversion.GetPeriodTotalsAsync(default, default).ReturnsForAnyArgs(
            new PeriodTotalsResult(new PeriodTotals("USD", 0m, 0m), [], IsConverted: false));

        await _sut.WritePdfAsync(await _sut.BuildAsync(Request()), _pdf);

        using var document = PdfReader.Open(_pdf, PdfDocumentOpenMode.Import);
        document.PageCount.Should().Be(1);
    }

    [Fact]
    public void A_single_day_shows_one_date()
    {
        var day = new DateTime(2026, 10, 16);
        PeriodReportPdf.PeriodText(new ReportRequest(day, day.AddDays(1).AddTicks(-1), PeriodType.Today, "USD", false))
            .Should().NotContain("–");
        PeriodReportPdf.PeriodText(Request()).Should().Contain("–");
    }
}
