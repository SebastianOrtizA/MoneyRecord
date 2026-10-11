using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IDataExportService _dataExportService;
        private readonly IErrorHandler _errorHandler;

        [ObservableProperty]
        private bool isExporting;

        [ObservableProperty]
        private bool isImporting;

        public SettingsViewModel(IDataExportService dataExportService, IErrorHandler errorHandler)
        {
            _dataExportService = dataExportService;
            _errorHandler = errorHandler;
        }

        [RelayCommand]
        private async Task SaveBackupLocalAsync()
        {
            try
            {
                IsExporting = true;

                var filePath = Path.Combine(_dataExportService.BackupsFolder, BackupFileName());
                await _dataExportService.ExportBackupAsync(filePath);

                await Toast.Make($"{AppResources.BackupCreatedSuccessfully}").Show();
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToCreateBackup);
            }
            finally
            {
                IsExporting = false;
            }
        }

        [RelayCommand]
        private async Task ShareBackupAsync()
        {
            try
            {
                IsExporting = true;

                // Backups include photos, so don't let earlier shared copies pile up in the cache
                foreach (var old in Directory.GetFiles(FileSystem.CacheDirectory, "MoneyRecord_Backup_*.zip"))
                    File.Delete(old);

                var filePath = Path.Combine(FileSystem.CacheDirectory, BackupFileName());
                await _dataExportService.ExportBackupAsync(filePath);

                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = AppResources.ShareBackup,
                    File = new ShareFile(filePath)
                });
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToCreateBackup);
            }
            finally
            {
                IsExporting = false;
            }
        }

        private static string BackupFileName() => $"MoneyRecord_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip";

        [RelayCommand]
        private async Task ExportCsvAsync()
        {
            try
            {
                IsExporting = true;

                var csv = await _dataExportService.ExportCsvAsync();
                var fileName = $"MoneyRecord_Transactions_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
                await File.WriteAllTextAsync(filePath, csv);

                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = AppResources.ShareTransactions,
                    File = new ShareFile(filePath)
                });

                await Toast.Make(AppResources.CsvExportedSuccessfully).Show();
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToExportCsv);
            }
            finally
            {
                IsExporting = false;
            }
        }

        [RelayCommand]
        private async Task RestoreAsync()
        {
            string? pickedCopy = null;
            try
            {
                string? backupPath;

                var backupDir = _dataExportService.BackupsFolder;
                var localBackups = Directory.Exists(backupDir)
                    ? Directory.GetFiles(backupDir, "*.zip").Concat(Directory.GetFiles(backupDir, "*.json"))
                        .OrderByDescending(File.GetLastWriteTimeUtc).ToArray()
                    : Array.Empty<string>();

                if (localBackups.Length > 0)
                {
                    var options = localBackups.Select(Path.GetFileName).ToList();
                    options.Add(AppResources.SelectBackupFile);

                    var choice = await Shell.Current.DisplayActionSheetAsync(
                        AppResources.RestoreFromBackup, AppResources.Cancel, null, options.ToArray());

                    if (choice == null || choice == AppResources.Cancel) return;

                    backupPath = choice == AppResources.SelectBackupFile
                        ? pickedCopy = await PickBackupFileAsync()
                        : localBackups.First(f => Path.GetFileName(f) == choice);
                }
                else
                {
                    backupPath = pickedCopy = await PickBackupFileAsync();
                }

                if (backupPath == null) return;

                var confirm = await Shell.Current.DisplayAlertAsync(
                    AppResources.ConfirmRestore,
                    AppResources.RestoreWarning,
                    AppResources.YesRestore,
                    AppResources.Cancel);

                if (!confirm) return;

                IsImporting = true;
                var result = await _dataExportService.RestoreAsync(backupPath);
                IsImporting = false;

                var undo = await Shell.Current.DisplayAlertAsync(
                    AppResources.RestoreComplete,
                    string.Format(AppResources.RestoreSuccessful, result.RecordCount, Path.GetFileName(result.SnapshotPath)),
                    AppResources.UndoRestore,
                    AppResources.OK);

                if (!undo) return;

                IsImporting = true;
                await _dataExportService.RestoreAsync(result.SnapshotPath);
                await Toast.Make(AppResources.RestoreUndone).Show();
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToRestore);
            }
            finally
            {
                IsImporting = false;
                if (pickedCopy != null)
                    File.Delete(pickedCopy);
            }
        }

        /// <summary>
        /// Lets the user pick a backup (.zip, or .json from an older version) and copies it to the cache folder.
        /// </summary>
        private static async Task<string?> PickBackupFileAsync()
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = AppResources.SelectBackupFile,
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, new[] { "application/zip", "application/x-zip-compressed", "application/json" } },
                    { DevicePlatform.iOS, new[] { "public.zip-archive", "public.json" } },
                    { DevicePlatform.WinUI, new[] { ".zip", ".json" } },
                    { DevicePlatform.macOS, new[] { "public.zip-archive", "public.json" } }
                })
            });

            if (result == null) return null;

            var copy = Path.Combine(FileSystem.CacheDirectory, $"restore_{Guid.NewGuid():N}{Path.GetExtension(result.FileName)}");
            using (var source = await result.OpenReadAsync())
            using (var target = File.Create(copy))
                await source.CopyToAsync(target);
            return copy;
        }
    }
}
