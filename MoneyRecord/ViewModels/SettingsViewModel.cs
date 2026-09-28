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

                var json = await _dataExportService.ExportJsonAsync();
                var fileName = $"MoneyRecord_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var backupDir = Path.Combine(FileSystem.AppDataDirectory, "Backups");
                Directory.CreateDirectory(backupDir);
                var filePath = Path.Combine(backupDir, fileName);
                await File.WriteAllTextAsync(filePath, json);

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

                var json = await _dataExportService.ExportJsonAsync();
                var fileName = $"MoneyRecord_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
                await File.WriteAllTextAsync(filePath, json);

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
        private async Task ImportJsonAsync()
        {
            try
            {
                string? json = null;

                var backupDir = Path.Combine(FileSystem.AppDataDirectory, "Backups");
                var localBackups = Directory.Exists(backupDir)
                    ? Directory.GetFiles(backupDir, "*.json").OrderByDescending(f => f).ToArray()
                    : Array.Empty<string>();

                if (localBackups.Length > 0)
                {
                    var options = localBackups.Select(Path.GetFileName).ToList();
                    options.Add(AppResources.SelectBackupFile);

                    var choice = await Shell.Current.DisplayActionSheetAsync(
                        AppResources.RestoreFromBackup, AppResources.Cancel, null, options.ToArray());

                    if (choice == null || choice == AppResources.Cancel) return;

                    if (choice == AppResources.SelectBackupFile)
                    {
                        json = await PickBackupFileAsync();
                    }
                    else
                    {
                        var filePath = localBackups.First(f => Path.GetFileName(f) == choice);
                        json = await File.ReadAllTextAsync(filePath);
                    }
                }
                else
                {
                    json = await PickBackupFileAsync();
                }

                if (string.IsNullOrEmpty(json)) return;

                var confirm = await Shell.Current.DisplayAlertAsync(
                    AppResources.ConfirmRestore,
                    AppResources.RestoreWarning,
                    AppResources.YesRestore,
                    AppResources.Cancel);

                if (!confirm) return;

                IsImporting = true;

                var count = await _dataExportService.ImportJsonAsync(json);
                await Toast.Make(string.Format(AppResources.RestoreSuccessful, count)).Show();
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToRestore);
            }
            finally
            {
                IsImporting = false;
            }
        }

        private static async Task<string?> PickBackupFileAsync()
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = AppResources.SelectBackupFile,
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, new[] { "application/json" } },
                    { DevicePlatform.iOS, new[] { "public.json" } },
                    { DevicePlatform.WinUI, new[] { ".json" } },
                    { DevicePlatform.macOS, new[] { "public.json" } }
                })
            });

            if (result == null) return null;

            using var stream = await result.OpenReadAsync();
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
    }
}
