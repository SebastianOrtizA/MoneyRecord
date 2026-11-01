using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>
    /// More → Quick entries: the templates behind the Home chips. A tap edits one in a sheet (name, type, amount, category,
    /// account); the arrows set the order the chips show in.
    /// </summary>
    public partial class QuickEntriesViewModel : ObservableObject
    {
        private readonly IQuickEntryService _quickEntries;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;

        private QuickEntry? _editing;
        private bool _loadingForm;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEmpty))]
        private List<QuickEntryItem> items = [];

        public bool IsEmpty => Items.Count == 0;

        // ---- Add / edit sheet ----

        [ObservableProperty]
        private bool isFormOpen;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFormEditing))]
        private string formTitle = string.Empty;

        public bool IsFormEditing => _editing != null;

        [ObservableProperty]
        private string formName = string.Empty;

        [ObservableProperty]
        private string formAmount = string.Empty;

        /// <summary>0 = Expense, 1 = Income.</summary>
        [ObservableProperty]
        private int formTypeIndex;

        public List<string> TypeNames => [AppResources.Expense, AppResources.Income];

        [ObservableProperty]
        private List<Category> formCategories = [];

        [ObservableProperty]
        private Category? formCategory;

        [ObservableProperty]
        private List<Account> formAccounts = [];

        [ObservableProperty]
        private Account? formAccount;

        /// <summary>The picked account's currency: the amount field formats in it.</summary>
        [ObservableProperty]
        private string formCurrencyCode = string.Empty;

        public QuickEntriesViewModel(IQuickEntryService quickEntries, ICategoryRepository categoryRepository,
            IAccountRepository accountRepository, IErrorHandler errorHandler, IDialogService dialogService)
        {
            _quickEntries = quickEntries;
            _categoryRepository = categoryRepository;
            _accountRepository = accountRepository;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
        }

        public Task InitializeAsync() => LoadCommand.ExecuteAsync(null);

        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadAsync(CancellationToken cancellationToken)
        {
            try
            {
                IsLoading = true;
                var all = await _quickEntries.GetAllAsync();
                cancellationToken.ThrowIfCancellationRequested();
                Items = all;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A newer load replaced this one
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadQuickEntries);
            }
            finally
            {
                // The newer load owns the spinner
                if (!cancellationToken.IsCancellationRequested)
                    IsLoading = false;
            }
        }

        [RelayCommand]
        private Task AddAsync() => OpenFormAsync(null);

        [RelayCommand]
        private Task OpenAsync(QuickEntryItem? item) => item == null ? Task.CompletedTask : OpenFormAsync(item.Entry);

        [RelayCommand]
        private Task MoveUpAsync(QuickEntryItem? item) => MoveAsync(item, up: true);

        [RelayCommand]
        private Task MoveDownAsync(QuickEntryItem? item) => MoveAsync(item, up: false);

        private async Task MoveAsync(QuickEntryItem? item, bool up)
        {
            if (item == null)
                return;

            try
            {
                await _quickEntries.MoveAsync(item.Id, up);
                await LoadCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToSaveQuickEntry);
            }
        }

        private async Task OpenFormAsync(QuickEntry? entry)
        {
            try
            {
                _loadingForm = true;
                _editing = entry;
                OnPropertyChanged(nameof(IsFormEditing));
                FormTitle = entry == null ? AppResources.NewQuickEntry : AppResources.EditQuickEntry;
                FormName = entry?.Name ?? string.Empty;
                FormTypeIndex = entry?.Type == TransactionType.Income ? 1 : 0;

                FormAccounts = await _accountRepository.GetOwnAsync();
                FormAccount = FormAccounts.FirstOrDefault(a => a.Id == entry?.AccountId)
                    ?? FormAccounts.FirstOrDefault(a => a.IsDefault) ?? FormAccounts.FirstOrDefault();
                FormCurrencyCode = FormAccount?.CurrencyCode ?? string.Empty;
                await LoadFormCategoriesAsync(entry?.CategoryId);
                FormAmount = entry == null || entry.AsksAmount
                    ? string.Empty
                    : MoneyFormatter.FormatForEntry(entry.Amount, FormCurrencyCode);
                IsFormOpen = true;
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadQuickEntries);
            }
            finally
            {
                _loadingForm = false;
            }
        }

        private async Task LoadFormCategoriesAsync(int? selectedId, CancellationToken cancellationToken = default)
        {
            var type = FormTypeIndex == 1 ? CategoryType.Income : CategoryType.Expense;
            // "Travel" holds trip expenses only
            var categories = (await _categoryRepository.GetByTypeAsync(type)).Where(c => !c.IsTravel).ToList();
            cancellationToken.ThrowIfCancellationRequested();
            FormCategories = categories;
            FormCategory = FormCategories.FirstOrDefault(c => c.Id == selectedId) ?? FormCategories.FirstOrDefault();
        }

        partial void OnFormTypeIndexChanged(int value)
        {
            // The categories of the other type don't fit
            if (!_loadingForm)
                ReloadFormCategoriesCommand.Execute(null);
        }

        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task ReloadFormCategoriesAsync(CancellationToken cancellationToken)
        {
            try
            {
                await LoadFormCategoriesAsync(null, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A newer toggle replaced this one
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadQuickEntries);
            }
        }

        partial void OnFormAccountChanged(Account? value) => FormCurrencyCode = value?.CurrencyCode ?? string.Empty;

        [RelayCommand]
        private async Task SaveFormAsync()
        {
            if (FormName.Trim().Length == 0)
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterQuickEntryName);
                return;
            }
            if (FormCategory == null || FormAccount == null)
            {
                await _errorHandler.HandleAsync(AppResources.PleaseSelectCategory);
                return;
            }

            var cents = string.IsNullOrWhiteSpace(FormAmount) ? 0 : Money.ToCents(AmountParser.Parse(FormAmount, FormCurrencyCode));
            var entry = _editing ?? new QuickEntry();
            entry.Name = FormName;
            entry.Type = FormTypeIndex == 1 ? TransactionType.Income : TransactionType.Expense;
            entry.AmountCents = cents;
            entry.CategoryId = FormCategory.Id;
            entry.AccountId = FormAccount.Id;

            try
            {
                await _quickEntries.SaveAsync(entry);
                CloseForm();
                await LoadCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToSaveQuickEntry);
            }
        }

        [RelayCommand]
        private async Task DeleteFormAsync()
        {
            if (_editing is not { } entry)
                return;

            if (!await _dialogService.ConfirmAsync(AppResources.DeleteButton, string.Format(AppResources.QuickEntryDeleteConfirm, entry.Name),
                    AppResources.DeleteButton, AppResources.Cancel))
                return;

            try
            {
                await _quickEntries.DeleteAsync(entry.Id);
                CloseForm();
                await LoadCommand.ExecuteAsync(null);
                await _dialogService.ToastAsync(AppResources.QuickEntryDeleted);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToSaveQuickEntry);
            }
        }

        [RelayCommand]
        public void CloseForm()
        {
            IsFormOpen = false;
            _editing = null;
        }

        /// <summary>Android back: closes the sheet if one is open. False = nothing to close.</summary>
        public bool CloseTopOverlay()
        {
            if (!IsFormOpen)
                return false;

            CloseForm();
            return true;
        }
    }
}
