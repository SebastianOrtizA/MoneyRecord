using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>
    /// The Reports tabs' Export button: a PDF report or a CSV of the transactions for what the tab shows, then the share
    /// sheet. Spending / Income show the shared period; Trends, Cash flow and Net worth the last
    /// <see cref="PeriodViewModel.MonthCount"/> months (pass <see cref="Months"/>). Amounts by category follow the reports'
    /// currency choice.
    /// </summary>
    public partial class ReportExportViewModel(
        PeriodViewModel period,
        IReportExportService reports,
        IDataExportService data,
        IFileSharer sharer,
        IDialogService dialogs,
        IErrorHandler errorHandler) : ObservableObject
    {
        /// <summary>The command parameter of the time-series reports.</summary>
        public const string Months = "Months";

        /// <summary>From the tap to the share sheet: the button is disabled, so a double tap opens one choice only.</summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
        private bool isExporting;

        /// <summary>What an export from a tab covers (see the class summary).</summary>
        public ReportRequest Request(bool wholeMonths)
        {
            if (!wholeMonths)
            {
                var (start, end) = period.Range;
                return new ReportRequest(start, end, period.SelectedPeriod?.Type ?? PeriodType.CalendarMonth,
                    period.ReportCurrency, period.IsConverted);
            }

            var (first, last) = DateRangeHelper.GetLastMonths(period.MonthCount, DateTime.Today);
            return new ReportRequest(first, last, PeriodType.CustomPeriod, period.ReportCurrency, period.IsConverted);
        }

        private bool CanExport() => !IsExporting;

        [RelayCommand(CanExecute = nameof(CanExport))]
        private async Task ExportAsync(string? window)
        {
            IsExporting = true;
            try
            {
                var request = Request(window == Months);
                var choice = await dialogs.ActionSheetAsync(
                    string.Format(AppResources.ExportReportFor, DateFormatter.FormatRange(request.Start, request.End)),
                    AppResources.Cancel, [AppResources.ExportPdfReport, AppResources.ExportPeriodCsv]);
                var pdf = choice == AppResources.ExportPdfReport;
                if (!pdf && choice != AppResources.ExportPeriodCsv)
                    return;

                string path;
                if (pdf)
                {
                    path = sharer.NewCacheFile("MoneyRecord_Report", ".pdf");
                    await reports.WritePdfAsync(await reports.BuildAsync(request), path);
                }
                else
                {
                    path = sharer.NewCacheFile("MoneyRecord_Transactions", ".csv");
                    await File.WriteAllTextAsync(path, await data.ExportCsvAsync(request.Start, request.End));
                }

                await sharer.ShareAsync(path, AppResources.ShareReport);
            }
            catch (Exception ex)
            {
                await errorHandler.HandleAsync(ex, AppResources.FailedToExportReport);
            }
            finally
            {
                IsExporting = false;
            }
        }
    }
}
