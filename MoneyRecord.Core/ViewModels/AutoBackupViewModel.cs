using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>
    /// Settings → Automatic backups: the switch (turning it on asks for a folder first), the folder, how often, how many
    /// to keep, the last backup's status and Back up now.
    /// </summary>
    public partial class AutoBackupViewModel : ObservableObject
    {
        private readonly IAutoBackupService _service;
        private readonly IAutoBackupSettings _settings;
        private readonly IBackupFolder _folder;
        private readonly IDialogService _dialogs;
        private readonly IErrorHandler _errors;
        private bool _folderChecked;

        public AutoBackupViewModel(IAutoBackupService service, IAutoBackupSettings settings, IBackupFolder folder,
            IDialogService dialogs, IErrorHandler errors)
        {
            _service = service;
            _settings = settings;
            _folder = folder;
            _dialogs = dialogs;
            _errors = errors;
            RefreshNames();
            status = StatusText();
        }

        /// <summary>Platforms with a folder picker (Android) show the card.</summary>
        public bool IsSupported => _folder.IsSupported;

        public bool Enabled
        {
            get => _settings.Enabled;
            set
            {
                if (value == _settings.Enabled)
                    return;
                if (value)
                {
                    // Needs a reachable folder; the switch stays off when the picker is cancelled
                    TurnOnCommand.Execute(null);
                    return;
                }
                _settings.Enabled = false;
                OnPropertyChanged();
                Status = StatusText();
            }
        }

        /// <summary>The chosen folder's name, or "Not chosen".</summary>
        [ObservableProperty]
        private string folderText = AppResources.BackupFolderNone;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanBackUpNow))]
        private bool hasFolder;

        /// <summary>"Last backup: Oct 16, 2026", "Backing up…", a failure, or a folder the app can't reach.</summary>
        [ObservableProperty]
        private string status;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsIdle), nameof(CanBackUpNow))]
        private bool isBusy;

        public bool IsIdle => !IsBusy;

        /// <summary>Back up now needs a reachable folder.</summary>
        public bool CanBackUpNow => HasFolder && !IsBusy;

        [ObservableProperty]
        private IReadOnlyList<BackupFrequencyOption> frequencyOptions = [];

        [ObservableProperty]
        private BackupFrequencyOption? selectedFrequency;

        [ObservableProperty]
        private IReadOnlyList<BackupKeepOption> keepOptions = [];

        [ObservableProperty]
        private BackupKeepOption? selectedKeep;

        /// <summary>The pickers' names in the current language (after a language change).</summary>
        public void RefreshNames()
        {
            FrequencyOptions = BackupFrequencyOption.All();
            KeepOptions = BackupKeepOption.All();
            SelectedFrequency = FrequencyOptions.First(o => o.Frequency == _settings.Frequency);
            SelectedKeep = KeepOptions.FirstOrDefault(o => o.Count == _settings.Keep) ?? KeepOptions[1];
            if (!IsBusy)
                Status = StatusText();
        }

        partial void OnSelectedFrequencyChanged(BackupFrequencyOption? value)
        {
            if (value != null)
                _settings.Frequency = value.Frequency;
        }

        partial void OnSelectedKeepChanged(BackupKeepOption? value)
        {
            if (value != null)
                _settings.Keep = value.Count;
        }

        /// <summary>Shows the folder (access may have been lost meanwhile) and the last backup (the daily job may have run).</summary>
        [RelayCommand]
        private async Task RefreshAsync()
        {
            if (!IsSupported)
                return;

            // The folder's provider may be a cloud app
            var name = await Task.Run(() => _folder.Name);
            _folderChecked = true;
            HasFolder = name != null;
            FolderText = name ?? AppResources.BackupFolderNone;
            if (!IsBusy)
                Status = StatusText();
        }

        [RelayCommand]
        private async Task TurnOnAsync()
        {
            if (await Task.Run(() => _folder.Name) == null)
            {
                // Picking a folder turns them on and makes the first backup
                await ChooseFolderAsync();
                return;
            }

            _settings.Enabled = true;
            OnPropertyChanged(nameof(Enabled));
            await RunAsync(_service.RunIfDueAsync);
        }

        [RelayCommand]
        private async Task ChooseFolderAsync()
        {
            var picked = await _folder.PickAsync();
            if (picked)
                _settings.Enabled = true;
            OnPropertyChanged(nameof(Enabled));
            await RefreshAsync();
            // The new folder gets a backup at once: it shows the choice works
            if (picked)
                await BackUpNowAsync();
        }

        [RelayCommand]
        private async Task BackUpNowAsync()
        {
            try
            {
                await RunAsync(_service.BackUpNowAsync);
                await _dialogs.ToastAsync(AppResources.BackupCreatedSuccessfully);
            }
            catch (Exception ex)
            {
                await _errors.HandleAsync(ex, AppResources.FailedToCreateBackup);
            }
        }

        private async Task RunAsync(Func<Task> backup)
        {
            IsBusy = true;
            Status = AppResources.BackingUp;
            try
            {
                await backup();
            }
            finally
            {
                IsBusy = false;
                await RefreshAsync();
            }
        }

        private string StatusText()
        {
            // On needs a folder (picked when turned on): none any more = access was lost
            if (_settings.Enabled && _folderChecked && !HasFolder)
                return AppResources.BackupFolderUnavailable;
            if (_settings.LastFailure is { } failed && (_settings.LastBackup is not { } last || failed > last))
                return string.Format(AppResources.BackupFailed, DateFormatter.Format(failed, DateStyle.Medium));
            return _settings.LastBackup is { } backup
                ? string.Format(AppResources.LastBackup, DateFormatter.Format(backup, DateStyle.Medium))
                : AppResources.NoBackupYet;
        }
    }
}
