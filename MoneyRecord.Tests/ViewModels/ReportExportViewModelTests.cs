using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.ViewModels;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace MoneyRecord.Tests.ViewModels;

public sealed class ReportExportViewModelTests : IDisposable
{
    private readonly PeriodViewModel _period = new(Substitute.For<IConversionService>());
    private readonly IReportExportService _reports = Substitute.For<IReportExportService>();
    private readonly IDataExportService _data = Substitute.For<IDataExportService>();
    private readonly IFileSharer _sharer = Substitute.For<IFileSharer>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IErrorHandler _errors = Substitute.For<IErrorHandler>();
    private readonly ReportExportViewModel _sut;
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"moneyrecord-export-{Guid.NewGuid():N}");

    public ReportExportViewModelTests()
    {
        Directory.CreateDirectory(_folder);
        _sut = new ReportExportViewModel(_period, _reports, _data, _sharer, _dialogs, _errors);
        _sharer.NewCacheFile(default!, default!).ReturnsForAnyArgs(call => Path.Combine(_folder, call.ArgAt<string>(0) + call.ArgAt<string>(1)));
        _data.ExportCsvAsync(default, default).ReturnsForAnyArgs("Date\n");
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private void Choose(string? option) =>
        _dialogs.ActionSheetAsync(default!, default!, default!).ReturnsForAnyArgs(option);

    [Fact]
    public void The_spending_and_income_tabs_export_the_shared_period()
    {
        _period.SelectedPeriod = _period.Periods.First(p => p.Type == PeriodType.LastMonth);

        var request = _sut.Request(wholeMonths: false);

        (request.Start, request.End).Should().Be(_period.Range);
        request.Period.Should().Be(PeriodType.LastMonth);
    }

    [Fact]
    public void The_time_series_tabs_export_their_last_months_as_a_custom_period()
    {
        _period.MonthCount = 3;
        var thisMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        var request = _sut.Request(wholeMonths: true);

        request.Start.Should().Be(thisMonth.AddMonths(-2));
        request.End.Should().Be(thisMonth.AddMonths(1).AddTicks(-1));
        request.Period.Should().Be(PeriodType.CustomPeriod);
    }

    [Fact]
    public async Task The_pdf_is_built_written_and_shared()
    {
        Choose(AppResources.ExportPdfReport);

        await _sut.ExportCommand.ExecuteAsync(null);

        var path = Path.Combine(_folder, "MoneyRecord_Report.pdf");
        await _reports.Received(1).WritePdfAsync(Arg.Any<PeriodReport>(), path);
        await _sharer.Received(1).ShareAsync(path, AppResources.ShareReport);
        _sut.IsExporting.Should().BeFalse();
    }

    [Fact]
    public async Task The_csv_has_the_requested_range()
    {
        Choose(AppResources.ExportPeriodCsv);
        var request = _sut.Request(wholeMonths: true);

        await _sut.ExportCommand.ExecuteAsync(ReportExportViewModel.Months);

        await _data.Received(1).ExportCsvAsync(request.Start, request.End);
        File.ReadAllText(Path.Combine(_folder, "MoneyRecord_Transactions.csv")).Should().Be("Date\n");
        await _sharer.Received(1).ShareAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Cancel")]
    public async Task Cancelling_the_choice_exports_nothing(string? option)
    {
        Choose(option == null ? null : AppResources.Cancel);

        await _sut.ExportCommand.ExecuteAsync(null);

        await _reports.DidNotReceiveWithAnyArgs().BuildAsync(default!);
        await _sharer.DidNotReceiveWithAnyArgs().ShareAsync(default!, default!);
    }

    [Fact]
    public async Task A_failure_is_reported_and_nothing_is_shared()
    {
        Choose(AppResources.ExportPdfReport);
        var failure = new IOException("disk full");
        _reports.BuildAsync(default!).ThrowsAsyncForAnyArgs(failure);

        await _sut.ExportCommand.ExecuteAsync(null);

        await _errors.Received(1).HandleAsync(failure, AppResources.FailedToExportReport);
        await _sharer.DidNotReceiveWithAnyArgs().ShareAsync(default!, default!);
        _sut.IsExporting.Should().BeFalse();
    }
}
