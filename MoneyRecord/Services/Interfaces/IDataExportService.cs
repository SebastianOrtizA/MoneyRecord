namespace MoneyRecord.Services.Interfaces
{
    public interface IDataExportService
    {
        Task<string> ExportJsonAsync();
        Task<string> ExportCsvAsync();
        Task<int> ImportJsonAsync(string json);
    }
}
