using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>Payees page: every payee with its use, a search, and rename / merge / delete from a tap.</summary>
    public partial class PayeesViewModel : ObservableObject
    {
        private readonly IPayeeService _payeeService;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;

        private List<PayeeSummary> _all = [];

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string searchText = string.Empty;

        partial void OnSearchTextChanged(string value) => ApplySearch();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(EmptyText), nameof(IsEmpty))]
        private List<PayeeSummary> payees = [];

        public bool IsEmpty => Payees.Count == 0;

        public string EmptyText => _all.Count == 0 ? AppResources.NoPayeesYet : AppResources.NoMatchingPayees;

        public PayeesViewModel(IPayeeService payeeService, IErrorHandler errorHandler, IDialogService dialogService)
        {
            _payeeService = payeeService;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
        }

        public Task InitializeAsync() => LoadPayeesCommand.ExecuteAsync(null);

        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadPayeesAsync(CancellationToken cancellationToken)
        {
            try
            {
                IsLoading = true;
                var all = await _payeeService.GetAllAsync();
                cancellationToken.ThrowIfCancellationRequested();

                _all = all;
                ApplySearch();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A newer load replaced this one
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadPayees);
            }
            finally
            {
                // The newer load owns the spinner
                if (!cancellationToken.IsCancellationRequested)
                    IsLoading = false;
            }
        }

        private void ApplySearch() => Payees = _all.Where(p => ListSearch.Matches(p.Name, SearchText)).ToList();

        [RelayCommand]
        private async Task OpenPayeeAsync(PayeeSummary? payee)
        {
            if (payee == null)
                return;

            var actions = new List<(string Label, Func<PayeeSummary, Task> Run)> { (AppResources.RenamePayee, RenameAsync) };
            // Merging needs another payee to merge into
            if (_all.Count > 1)
                actions.Add((AppResources.MergePayee, ChooseMergeTargetAsync));
            actions.Add((AppResources.DeletePayee, DeleteAsync));

            var labels = actions.Select(a => a.Label).ToList();
            var picked = await _dialogService.ActionSheetAsync(payee.Name, AppResources.Cancel, labels);
            var index = picked == null ? -1 : labels.IndexOf(picked);
            if (index < 0)
                return;

            try
            {
                await actions[index].Run(payee);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToSavePayee);
            }
        }

        private async Task RenameAsync(PayeeSummary payee)
        {
            var typed = await _dialogService.PromptAsync(AppResources.RenamePayee,
                string.Format(AppResources.RenamePayeeMessage, payee.Name), AppResources.Save, AppResources.Cancel, payee.Name);
            var name = PayeeNames.Normalize(typed);
            if (name.Length == 0 || name == payee.Name)
                return;

            // The name of another payee: the user means the same one, so offer to merge them
            if (_all.FirstOrDefault(p => p.Id != payee.Id && PayeeNames.Same(p.Name, name)) is { } existing)
            {
                if (await _dialogService.ConfirmAsync(AppResources.PayeeExistsTitle,
                        string.Format(AppResources.PayeeExistsMergeMessage, payee.Name, existing.Name),
                        AppResources.MergeButton, AppResources.Cancel))
                    await MergeAsync(payee, existing);
                return;
            }

            await _payeeService.RenameAsync(payee.Id, name);
            await LoadPayeesCommand.ExecuteAsync(null);
        }

        private async Task ChooseMergeTargetAsync(PayeeSummary payee)
        {
            var targets = _all.Where(p => p.Id != payee.Id).ToList();
            var labels = PickList.UniqueLabels(targets.Select(p => p.Name));
            var picked = await _dialogService.ActionSheetAsync(string.Format(AppResources.MergePayeeInto, payee.Name),
                AppResources.Cancel, labels);
            var index = picked == null ? -1 : labels.IndexOf(picked);
            if (index < 0)
                return;

            var target = targets[index];
            if (await _dialogService.ConfirmAsync(AppResources.MergePayee,
                    string.Format(AppResources.MergePayeeConfirmMessage, payee.Name, target.Name, payee.UsesText),
                    AppResources.MergeButton, AppResources.Cancel))
                await MergeAsync(payee, target);
        }

        private async Task MergeAsync(PayeeSummary payee, PayeeSummary target)
        {
            await _payeeService.MergeAsync(payee.Id, target.Id);
            await LoadPayeesCommand.ExecuteAsync(null);
            await _dialogService.ToastAsync(AppResources.PayeesMerged);
        }

        private async Task DeleteAsync(PayeeSummary payee)
        {
            // A debt needs its person: delete the debts first, or merge the payee into another
            if (payee.DebtCount > 0)
            {
                await _dialogService.AlertAsync(AppResources.DeletePayee, string.Format(AppResources.PayeeHasDebts, payee.Name));
                return;
            }

            if (!await _dialogService.ConfirmAsync(AppResources.DeletePayee,
                    string.Format(AppResources.DeletePayeeConfirmation, payee.Name, payee.UsesText),
                    AppResources.DeleteButton, AppResources.Cancel))
                return;

            await _payeeService.DeleteAsync(payee.Id);
            await LoadPayeesCommand.ExecuteAsync(null);
            await _dialogService.ToastAsync(AppResources.PayeeDeleted);
        }
    }
}
