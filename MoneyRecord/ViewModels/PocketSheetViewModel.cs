using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using System.Collections.ObjectModel;

namespace MoneyRecord.ViewModels
{
    /// <summary>
    /// The pocket sheet of the Accounts page: name + icon, set aside / take out, the movements and delete. The page's view
    /// model opens it for an account and reloads its list whenever <see cref="Changed"/> is called.
    /// </summary>
    public partial class PocketSheetViewModel : ObservableObject
    {
        private readonly IPocketService _pocketService;
        private readonly IBalanceService _balanceService;
        private readonly ICategoryIconService _categoryIconService;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;

        // The sheet's account (set for a new pocket too)
        private Account? _account;

        /// <summary>Called after a pocket or its money changed, so the account list can reload.</summary>
        public Func<Task>? Changed { get; set; }

        [ObservableProperty]
        private bool isOpen;

        /// <summary>The pocket the sheet edits; null when it adds a new one.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEdit), nameof(Title), nameof(SaveText))]
        private Pocket? editing;

        public bool IsEdit => Editing != null;

        public string Title => IsEdit ? AppResources.EditPocket : AppResources.NewPocket;

        public string SaveText => IsEdit ? AppResources.Save : AppResources.NewPocket;

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string iconCode = string.Empty;

        [ObservableProperty]
        private ObservableCollection<AccountIcon> icons = new();

        /// <summary>The amount to set aside or take out.</summary>
        [ObservableProperty]
        private string amount = string.Empty;

        /// <summary>The pocket's account currency: the amount field formats in it.</summary>
        [ObservableProperty]
        private string currencyCode = string.Empty;

        /// <summary>"In this pocket: $500.00".</summary>
        [ObservableProperty]
        private string holdsText = string.Empty;

        /// <summary>"Available in Bank: $1,500.00".</summary>
        [ObservableProperty]
        private string availableText = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasMovements))]
        private List<PocketMovementRow> movements = [];

        public bool HasMovements => Movements.Count > 0;

        public PocketSheetViewModel(IPocketService pocketService, IBalanceService balanceService, ICategoryIconService categoryIconService,
            IErrorHandler errorHandler, IDialogService dialogService)
        {
            _pocketService = pocketService;
            _balanceService = balanceService;
            _categoryIconService = categoryIconService;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
        }

        /// <summary>Opens the sheet for a new pocket of <paramref name="account"/>, or edits <paramref name="pocket"/>.</summary>
        public async Task OpenAsync(Account account, Pocket? pocket)
        {
            if (Icons.Count == 0)
                Icons = new ObservableCollection<AccountIcon>(_categoryIconService.GetAccountIcons());

            _account = account;
            Editing = pocket;
            Name = pocket?.Name ?? string.Empty;
            Amount = string.Empty;
            CurrencyCode = MoneyFormatter.ResolveCurrencyCode(account.CurrencyCode);
            SelectIcon(pocket?.IconCode ?? new Pocket().IconCode);
            await RefreshAsync();
            IsOpen = true;
        }

        [RelayCommand]
        private void SelectIcon(string code)
        {
            IconCode = code;
            foreach (var icon in Icons)
                icon.IsSelected = icon.Code == code;
        }

        /// <summary>What the pocket holds, what its account has available, and the movements.</summary>
        private async Task RefreshAsync()
        {
            var account = _account;
            if (account == null)
                return;

            try
            {
                // One read of the account's pockets gives the pocket's balance and what all of them hold
                var pockets = await _pocketService.GetPocketsAsync(account.Id);
                var available = await _balanceService.GetAccountBalanceAsync(account.Id) - pockets.Sum(p => p.Balance);
                AvailableText = string.Format(AppResources.PocketAccountAvailable, account.Name,
                    MoneyFormatter.Format(available, CurrencyCode));

                if (Editing is { } pocket)
                {
                    var held = pockets.FirstOrDefault(p => p.PocketId == pocket.Id)?.Balance ?? 0m;
                    HoldsText = string.Format(AppResources.PocketHolds, MoneyFormatter.Format(held, CurrencyCode));
                    Movements = (await _pocketService.GetMovementsAsync(pocket.Id))
                        .Select(m => new PocketMovementRow(m, CurrencyCode)).ToList();
                }
                else
                {
                    HoldsText = string.Empty;
                    Movements = [];
                }
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadAccountBalances);
            }
        }

        [RelayCommand]
        public void Close()
        {
            IsOpen = false;
            Editing = null;
            _account = null;
        }

        private Task NotifyChangedAsync() => Changed?.Invoke() ?? Task.CompletedTask;

        /// <summary>Saves the name and icon. A new pocket stays open, ready to have money set aside.</summary>
        [RelayCommand]
        private async Task SaveAsync()
        {
            var account = _account;
            var trimmed = Name.Trim();
            if (account == null)
                return;
            if (trimmed.Length == 0)
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterPocketName);
                return;
            }

            var adding = Editing == null;
            var pocket = Editing ?? new Pocket { AccountId = account.Id, CreatedDate = DateTime.Now };
            pocket.Name = trimmed;
            pocket.IconCode = string.IsNullOrEmpty(IconCode) ? pocket.IconCode : IconCode;
            try
            {
                await _pocketService.SaveAsync(pocket);
                await NotifyChangedAsync();
                if (adding)
                {
                    Editing = pocket;
                    await RefreshAsync();
                }
                else
                {
                    Close();
                }
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToSavePocket);
            }
        }

        [RelayCommand]
        private Task SetAsideAsync() => MoveAsync(1);

        [RelayCommand]
        private Task TakeOutAsync() => MoveAsync(-1);

        private async Task MoveAsync(int sign)
        {
            var pocket = Editing;
            var account = _account;
            if (pocket == null || account == null)
                return;

            var value = AmountParser.Parse(Amount, CurrencyCode);
            if (value <= 0 || !Money.IsInRange(value))
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterValidAmount);
                return;
            }

            try
            {
                await _pocketService.MoveAsync(pocket.Id, sign * value, DateTime.Now);
                Amount = string.Empty;
                await Task.WhenAll(RefreshAsync(), NotifyChangedAsync());
            }
            catch (PocketProblemException ex)
            {
                await _errorHandler.HandleAsync(ex.Problem == PocketProblem.NotEnoughAvailable
                    ? string.Format(AppResources.PocketNotEnoughAvailable, account.Name)
                    : AppResources.PocketNotEnoughInPocket);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToMovePocket);
            }
        }

        /// <summary>Deletes the pocket in the sheet; the sheet stays open behind the question, so cancelling returns to it.</summary>
        [RelayCommand]
        private async Task DeleteAsync()
        {
            var pocket = Editing;
            var account = _account;
            if (pocket == null || account == null)
                return;

            if (!await _dialogService.ConfirmAsync(AppResources.ConfirmDelete,
                    string.Format(AppResources.DeletePocketConfirmation, pocket.Name, account.Name),
                    AppResources.YesDelete, AppResources.Cancel))
                return;

            try
            {
                await _pocketService.DeleteAsync(pocket.Id);
                Close();
                await NotifyChangedAsync();
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToDeletePocket);
            }
        }
    }
}
