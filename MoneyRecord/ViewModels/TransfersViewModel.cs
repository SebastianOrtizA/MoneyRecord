using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Views;
using System.Collections.ObjectModel;

namespace MoneyRecord.ViewModels
{
    public partial class TransfersViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        [ObservableProperty]
        private ObservableCollection<Transfer> transfers = new();

        [ObservableProperty]
        private bool isRefreshing = false;

        [ObservableProperty]
        private bool hasTransfers = false;

        public TransfersViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task InitializeAsync()
        {
            await LoadTransfersAsync();
        }

        [RelayCommand]
        private async Task LoadTransfersAsync()
        {
            try
            {
                IsRefreshing = true;

                var transferList = await _databaseService.GetTransfersAsync() ?? new List<Transfer>();
                transferList = transferList.OrderByDescending(t => t.Date).ToList();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Transfers.Clear();
                    foreach (var transfer in transferList)
                    {
                        Transfers.Add(transfer);
                    }
                    HasTransfers = Transfers.Any();
                });
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync(AppResources.Error, string.Format(AppResources.FailedToLoadTransfers, ex.Message), AppResources.OK);
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        [RelayCommand]
        private async Task AddTransferAsync()
        {
            await Shell.Current.GoToAsync(nameof(AddTransferPage));
        }

        [RelayCommand]
        private async Task EditTransferAsync(Transfer transfer)
        {
            if (transfer == null)
                return;

            await Shell.Current.GoToAsync(nameof(AddTransferPage), new Dictionary<string, object>
            {
                { "Transfer", transfer }
            });
        }

        [RelayCommand]
        private async Task DeleteTransferAsync(Transfer transfer)
        {
            if (transfer == null)
                return;

            var confirm = await Shell.Current.DisplayAlertAsync(
                AppResources.ConfirmDelete,
                string.Format(AppResources.DeleteTransferConfirmMessage, transfer.SourceAccountName, transfer.DestinationAccountName, transfer.Amount),
                AppResources.YesDelete,
                AppResources.Cancel);

            if (!confirm)
                return;

            try
            {
                await _databaseService.DeleteTransferAsync(transfer);
                await LoadTransfersAsync();
                await Shell.Current.DisplayAlertAsync(AppResources.Success, AppResources.TransferDeletedSuccessfully, AppResources.OK);
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync(AppResources.Error, string.Format(AppResources.FailedToDeleteTransfer, ex.Message), AppResources.OK);
            }
        }
    }
}
