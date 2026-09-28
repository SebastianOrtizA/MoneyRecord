using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Views;
using System.Collections.ObjectModel;

namespace MoneyRecord.ViewModels
{
    public partial class TransfersViewModel : ObservableObject
    {
        private readonly ITransactionEnrichmentService _enrichmentService;
        private readonly ITransferRepository _transferRepository;
        private readonly IErrorHandler _errorHandler;

        [ObservableProperty]
        private ObservableCollection<Transfer> transfers = new();

        [ObservableProperty]
        private bool isRefreshing = false;

        [ObservableProperty]
        private bool hasTransfers = false;

        public TransfersViewModel(ITransactionEnrichmentService enrichmentService, ITransferRepository transferRepository, IErrorHandler errorHandler)
        {
            _enrichmentService = enrichmentService;
            _transferRepository = transferRepository;
            _errorHandler = errorHandler;
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

                var transferList = await _enrichmentService.GetAllEnrichedTransfersAsync() ?? new List<Transfer>();
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
                await _errorHandler.HandleAsync(ex, string.Format(AppResources.FailedToLoadTransfers, ex.Message));
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
                await _transferRepository.DeleteAsync(transfer);
                await LoadTransfersAsync();
                await Toast.Make(AppResources.TransferDeletedSuccessfully).Show();
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, string.Format(AppResources.FailedToDeleteTransfer, ex.Message));
            }
        }
    }
}
