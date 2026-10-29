using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>
    /// Support MoneyRecord: the tips the store sells (<see cref="ITipJar"/>, the Google Play build only; every other build
    /// hides the screen). A tip unlocks nothing.
    /// </summary>
    public partial class SupportViewModel(
        ITipJar tipJar,
        IDialogService dialogs,
        IErrorHandler errorHandler) : ObservableObject
    {
        public bool HasTips => Tips.Count > 0;

        /// <summary>The store is there but sold nothing (offline, products not active yet).</summary>
        public bool ShowTipsUnavailable => IsLoaded && Tips.Count == 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasTips), nameof(ShowTipsUnavailable))]
        private IReadOnlyList<TipOption> tips = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowTipsUnavailable))]
        private bool isLoaded;

        /// <summary>From the tap to the store's answer: the buttons are disabled, so a double tap pays once.</summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(TipCommand))]
        private bool isBusy;

        [RelayCommand]
        private async Task LoadAsync()
        {
            Tips = await tipJar.GetTipsAsync();
            IsLoaded = true;
        }

        private bool CanTip() => !IsBusy;

        [RelayCommand(CanExecute = nameof(CanTip))]
        private async Task TipAsync(TipOption tip)
        {
            IsBusy = true;
            try
            {
                switch (await tipJar.TipAsync(tip.ProductId))
                {
                    case TipOutcome.Thanked:
                        await dialogs.ToastAsync(AppResources.SupportThanks);
                        break;
                    case TipOutcome.Pending:
                        await dialogs.ToastAsync(AppResources.TipPending);
                        break;
                    case TipOutcome.Failed:
                        await errorHandler.HandleAsync(AppResources.FailedToTip);
                        break;
                }
            }
            catch (Exception ex)
            {
                await errorHandler.HandleAsync(ex, AppResources.FailedToTip);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
