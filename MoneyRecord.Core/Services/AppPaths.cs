using MoneyRecord.Helpers;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class AppPaths(string appDataDirectory) : IAppPaths
    {
        public const string DatabaseFileName = "moneyrecord.db3";
        public const string BackupsFolderName = "Backups";
        public const string LogsFolderName = "logs";

        public string DatabasePath => Path.Combine(appDataDirectory, DatabaseFileName);

        public string ReceiptsFolder => Path.Combine(appDataDirectory, ReceiptFiles.FolderName);

        public string BackupsFolder => Path.Combine(appDataDirectory, BackupsFolderName);

        public string LogsFolder => Path.Combine(appDataDirectory, LogsFolderName);
    }
}
