using MoneyRecord.Helpers;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class AppPaths(string appDataDirectory) : IAppPaths
    {
        public const string DatabaseFileName = "moneyrecord.db3";

        public string AppDataDirectory { get; } = appDataDirectory;

        public string DatabasePath => Path.Combine(AppDataDirectory, DatabaseFileName);

        public string ReceiptsFolder => Path.Combine(AppDataDirectory, ReceiptFiles.FolderName);

        public string BackupsFolder => Path.Combine(AppDataDirectory, "Backups");
    }
}
