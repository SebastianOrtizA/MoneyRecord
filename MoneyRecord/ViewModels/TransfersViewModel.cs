using MoneyRecord.Helpers;
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
        private readonly ILedgerService _ledgerService;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;
        private readonly INavigationService _navigationService;

        [ObservableProperty]
        private ObservableCollection<Transfer> transfers = new();

        [ObservableProperty]
        private bool isRefreshing = false;

        [ObservableProperty]
        private bool hasTransfers = false;

        public TransfersViewModel(ITransactionEnrichmentService enrichmentService, ILedgerService ledgerService, IErrorHandler errorHandler, IDialogService dialogService, INavigationService navigationService)
        {
            _enrichmentService = enrichmentService;
            _ledgerService = ledgerService;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
            _navigationService = navigationService;
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
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadTransfers);
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        [RelayCommand]
        private async Task AddTransferAsync()
        {
            await _navigationService.NavigateToAsync(nameof(AddTransferPage));
        }

        [RelayCommand]
        private async Task EditTransferAsync(Transfer transfer)
        {
            if (transfer == null)
                return;

            await _navigationService.NavigateToAsync(nameof(AddTransferPage), new Dictionary<string, object>
            {
                { "Transfer", transfer }
            });
        }

        [RelayCommand]
        private async Task DeleteTransferAsync(Transfer transfer)
        {
            if (transfer == null)
                return;

            try
            {
                var deleted = await _ledgerService.DeleteTransferAsync(transfer.Id);
                await LoadTransfersAsync();

                if (deleted != null)
                    await _dialogService.ToastWithUndoAsync(AppResources.TransferDeletedUndo, () => UndoDeleteAsync(deleted), () =>
                    {
                        _ledgerService.Discard(deleted);
                        return Task.CompletedTask;
                    });
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToDeleteTransfer);
            }
        }

        private async Task UndoDeleteAsync(DeletedEntry deleted)
        {
            try
            {
                if (await _ledgerService.RestoreAsync(deleted))
                {
                    await LoadTransfersAsync();
                    return;
                }

                _ledgerService.Discard(deleted);
                await _dialogService.ToastAsync(AppResources.UndoNotPossible);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.UndoNotPossible);
            }
        }
    }
}
