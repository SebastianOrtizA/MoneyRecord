using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Views;

namespace MoneyRecord.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IDataExportService _dataExportService;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;
        private readonly ICurrencyService _currencyService;
        private readonly INavigationService _navigationService;
        private readonly IThemeService _themeService;
        private readonly IPreferencesService _preferences;
        private readonly LocalizationService _localization;
        private readonly IAppPaths _paths;
        private readonly ICrashReportConsent _crashReports;

        [ObservableProperty]
        private bool isExporting;

        [ObservableProperty]
        private bool isImporting;

        public List<Currency> AvailableCurrencies { get; }

        /// <summary>The base currency: totals and reports are shown in it.</summary>
        [ObservableProperty]
        private Currency? baseCurrency;

        public SettingsViewModel(IDataExportService dataExportService, IErrorHandler errorHandler, IDialogService dialogService,
            ICurrencyService currencyService, INavigationService navigationService, IThemeService themeService,
            IPreferencesService preferences, LocalizationService localization, IAppPaths paths,
            ICrashReportConsent crashReports)
        {
            _dataExportService = dataExportService;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
            _currencyService = currencyService;
            _navigationService = navigationService;
            _themeService = themeService;
            _preferences = preferences;
            _localization = localization;
            _paths = paths;
            _crashReports = crashReports;
            AvailableCurrencies = currencyService.GetAvailableCurrencies();
            baseCurrency = currencyService.GetCurrency(currencyService.GetBaseCurrencyCode());
        }

        [ObservableProperty]
        private IReadOnlyList<LanguageOption> languages = LanguageOption.All();

        [ObservableProperty]
        private LanguageOption? selectedLanguage;

        [ObservableProperty]
        private IReadOnlyList<ThemeOption> themes = ThemeOption.All();

        [ObservableProperty]
        private ThemeOption? selectedTheme;

        /// <summary>Shows the saved settings again: the setup assistant may have changed the language or currency.</summary>
        public void Refresh()
        {
            BaseCurrency = _currencyService.GetCurrency(_currencyService.GetBaseCurrencyCode());
            SelectPickers();
        }

        private void SelectPickers()
        {
            SelectedLanguage = Languages.First(l => l.Code == _localization.LanguageSetting);
            SelectedTheme = Themes.First(t => t.Choice == _themeService.Current);
        }

        partial void OnSelectedLanguageChanged(LanguageOption? value)
        {
            if (value == null || value.Code == _localization.LanguageSetting)
                return;

            _localization.SetLanguage(value.Code, _preferences);

            // The pickers' "System default" / theme names are translated too
            Languages = LanguageOption.All();
            Themes = ThemeOption.All();
            SelectPickers();
        }

        partial void OnSelectedThemeChanged(ThemeOption? value)
        {
            if (value != null && value.Choice != _themeService.Current)
                _themeService.Set(value.Choice);
        }

        /// <summary>Only builds that can send reports (CI builds with the Sentry DSN) show the switch.</summary>
        public bool CrashReportsAvailable => _crashReports.IsAvailable;

        public bool CrashReportsEnabled
        {
            get => _crashReports.Enabled;
            set
            {
                if (value == _crashReports.Enabled)
                    return;
                _crashReports.Enabled = value;
                OnPropertyChanged();
            }
        }

        [RelayCommand]
        private Task OpenAboutAsync() => _navigationService.NavigateToAsync(nameof(AboutPage));

        partial void OnBaseCurrencyChanged(Currency? value)
        {
            if (value != null)
                _currencyService.SetBaseCurrencyCode(value.Code);
        }

        [RelayCommand]
        private Task OpenSetupAsync() => _navigationService.NavigateToAsync(nameof(SetupPage));

        [RelayCommand]
        private async Task SaveBackupLocalAsync()
        {
            try
            {
                IsExporting = true;

                var filePath = Path.Combine(_dataExportService.BackupsFolder, BackupFileName());
                await _dataExportService.ExportBackupAsync(filePath);

                await _dialogService.ToastAsync(AppResources.BackupCreatedSuccessfully);
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

                var filePath = NewCacheFile("MoneyRecord_Backup", ".zip");
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

        /// <summary>
        /// A new timestamped file in the cache folder, to share. Earlier ones with the same prefix are deleted first, so
        /// shared copies (backups include photos) don't pile up.
        /// </summary>
        private static string NewCacheFile(string prefix, string extension)
        {
            foreach (var old in Directory.GetFiles(FileSystem.CacheDirectory, $"{prefix}_*{extension}"))
                File.Delete(old);
            return Path.Combine(FileSystem.CacheDirectory, $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}");
        }

        [RelayCommand]
        private async Task ShareLogsAsync()
        {
            try
            {
                IsExporting = true;

                var filePath = NewCacheFile("MoneyRecord_Logs", ".zip");
                // Up to several MB of text to compress: off the UI thread
                if (!await Task.Run(() => LogFiles.CreateArchive(_paths.LogsFolder, filePath)))
                {
                    await _dialogService.ToastAsync(AppResources.NoLogsYet);
                    return;
                }

                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = AppResources.ShareLogs,
                    File = new ShareFile(filePath)
                });
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToShareLogs);
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
                var filePath = NewCacheFile("MoneyRecord_Transactions", ".csv");
                await File.WriteAllTextAsync(filePath, csv);

                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = AppResources.ShareTransactions,
                    File = new ShareFile(filePath)
                });

                await _dialogService.ToastAsync(AppResources.CsvExportedSuccessfully);
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
                    var options = localBackups.Select(f => Path.GetFileName(f)).ToList();
                    options.Add(AppResources.SelectBackupFile);

                    var choice = await _dialogService.ActionSheetAsync(AppResources.RestoreFromBackup, AppResources.Cancel, options);

                    if (choice == null) return;

                    backupPath = choice == AppResources.SelectBackupFile
                        ? pickedCopy = await PickBackupFileAsync()
                        : localBackups.First(f => Path.GetFileName(f) == choice);
                }
                else
                {
                    backupPath = pickedCopy = await PickBackupFileAsync();
                }

                if (backupPath == null) return;

                var confirm = await _dialogService.ConfirmAsync(
                    AppResources.ConfirmRestore,
                    AppResources.RestoreWarning,
                    AppResources.YesRestore,
                    AppResources.Cancel);

                if (!confirm) return;

                IsImporting = true;
                var result = await _dataExportService.RestoreAsync(backupPath);
                IsImporting = false;

                var undo = await _dialogService.ConfirmAsync(
                    AppResources.RestoreComplete,
                    string.Format(AppResources.RestoreSuccessful, result.RecordCount, Path.GetFileName(result.SnapshotPath)),
                    AppResources.UndoRestore,
                    AppResources.OK);

                if (!undo) return;

                IsImporting = true;
                await _dataExportService.RestoreAsync(result.SnapshotPath);
                await _dialogService.ToastAsync(AppResources.RestoreUndone);
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
