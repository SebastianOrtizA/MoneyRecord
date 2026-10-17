using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Pdf;

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

        [ObservableProperty]
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

            // The same window as the time-series reports: the last N months, this one included
            var today = DateTime.Today;
            var thisMonth = new DateTime(today.Year, today.Month, 1);
            return new ReportRequest(thisMonth.AddMonths(-(period.MonthCount - 1)), thisMonth.AddMonths(1).AddTicks(-1),
                PeriodType.CustomPeriod, period.ReportCurrency, period.IsConverted);
        }

        [RelayCommand]
        private async Task ExportAsync(string? window)
        {
            if (IsExporting)
                return;

            var request = Request(window == Months);
            var choice = await dialogs.ActionSheetAsync(string.Format(AppResources.ExportReportFor, PeriodReportPdf.PeriodText(request)),
                AppResources.Cancel, [AppResources.ExportPdfReport, AppResources.ExportPeriodCsv]);
            var pdf = choice == AppResources.ExportPdfReport;
            if (!pdf && choice != AppResources.ExportPeriodCsv)
                return;

            try
            {
                IsExporting = true;
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
