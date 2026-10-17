using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>The report export (Reports → Export): a period's report gathered, then written as a PDF.</summary>
    public interface IReportExportService
    {
        /// <summary>
        /// Totals per currency (converted or not, per the "Show in" switch, like Home) with the period before, the category
        /// shares of both types in the requested currency, the budgets, the own accounts' balances at the end and every
        /// transaction and transfer, oldest first.
        /// </summary>
        Task<PeriodReport> BuildAsync(ReportRequest request);

        /// <summary>Writes the report as a PDF (off the calling thread: a long period lays out many pages).</summary>
        Task WritePdfAsync(PeriodReport report, string path);
    }
}
