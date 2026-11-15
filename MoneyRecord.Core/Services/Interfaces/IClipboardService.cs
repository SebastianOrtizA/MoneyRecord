namespace MoneyRecord.Services.Interfaces
{
    /// <summary>The system clipboard (MAUI's, behind an interface so view models stay free of MAUI).</summary>
    public interface IClipboardService
    {
        Task SetTextAsync(string text);
    }
}
