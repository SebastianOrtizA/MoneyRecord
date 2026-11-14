namespace MoneyRecord.Services.Interfaces
{
    /// <summary>Opens a file the app stored (an attached PDF or photo) in the app the phone uses for it.</summary>
    public interface IFileOpener
    {
        Task OpenAsync(string path, string title);
    }
}
