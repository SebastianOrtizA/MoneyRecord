using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>Goals page: one card per goal (a tap opens the edit sheet) and the add/edit sheet.</summary>
    public partial class GoalsViewModel : ObservableObject
    {
        private readonly ISavingsGoalService _goalService;
        private readonly IAccountRepository _accountRepository;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;

        private List<Account> _accounts = [];
        // Account ID → the goal it belongs to
        private Dictionary<int, int> _linked = [];
        // The ticked accounts: kept across a currency change, so switching back restores them
        private HashSet<int> _selectedIds = [];

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasGoals))]
        private List<GoalProgress> goals = [];

        public bool HasGoals => Goals.Count > 0;

        /// <summary>The add/edit sheet is open.</summary>
        [ObservableProperty]
        private bool isFormOpen;

        /// <summary>The goal the sheet edits; null when it adds one.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEditMode), nameof(FormTitle), nameof(SaveText), nameof(RemovedAccountsText), nameof(HasRemovedAccounts))]
        private GoalProgress? editingGoal;

        public bool IsEditMode => EditingGoal != null;

        public string FormTitle => IsEditMode ? AppResources.EditGoal : AppResources.AddGoal;

        public string SaveText => IsEditMode ? AppResources.Save : AppResources.AddGoal;

        /// <summary>
        /// Linked accounts that moved to another currency can't count and aren't offered (the list holds the goal's currency),
        /// so saving drops them on purpose; the sheet says so first.
        /// </summary>
        public string RemovedAccountsText => EditingGoal is { HasUncounted: true } goal
            ? string.Format(AppResources.GoalAccountsRemovedOnSave, goal.UncountedAccountNames)
            : string.Empty;

        public bool HasRemovedAccounts => RemovedAccountsText.Length > 0;

        [ObservableProperty]
        private string formName = string.Empty;

        [ObservableProperty]
        private string formAmount = string.Empty;

        /// <summary>The goal's currency; only accounts in it are offered.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(NoFreeAccountsText))]
        private string formCurrency = string.Empty;

        partial void OnFormCurrencyChanged(string value) => BuildAccountChoices();

        /// <summary>The currencies of the accounts, base currency first.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowCurrencyPicker))]
        private List<string> currencies = [];

        public bool ShowCurrencyPicker => Currencies.Count > 1;

        [ObservableProperty]
        private bool hasTargetDate;

        [ObservableProperty]
        private DateTime targetDate = DateTime.Today.AddYears(1);

        /// <summary>The accounts in the goal's currency that no other goal has.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasAccountChoices))]
        private List<GoalAccountChoice> accountChoices = [];

        public bool HasAccountChoices => AccountChoices.Count > 0;

        public string NoFreeAccountsText => string.Format(AppResources.GoalNoFreeAccounts, FormCurrency);

        public GoalsViewModel(ISavingsGoalService goalService, IAccountRepository accountRepository, IErrorHandler errorHandler,
            IDialogService dialogService)
        {
            _goalService = goalService;
            _accountRepository = accountRepository;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
        }

        public Task InitializeAsync() => LoadGoalsCommand.ExecuteAsync(null);

        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadGoalsAsync(CancellationToken cancellationToken)
        {
            try
            {
                IsLoading = true;

                var goalsTask = _goalService.GetProgressAsync();
                var accountsTask = _accountRepository.GetAllAsync();
                var linkedTask = _goalService.GetLinkedAccountsAsync();
                await Task.WhenAll(goalsTask, accountsTask, linkedTask);
                cancellationToken.ThrowIfCancellationRequested();

                _accounts = (await accountsTask).OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
                _linked = await linkedTask;
                var baseCode = MoneyFormatter.BaseCurrency;
                Currencies = _accounts
                    .Select(a => MoneyFormatter.ResolveCurrencyCode(a.CurrencyCode))
                    .Distinct()
                    .OrderBy(code => code != baseCode)
                    .ThenBy(code => code, StringComparer.Ordinal)
                    .ToList();
                Goals = await goalsTask;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A newer load replaced this one
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadGoals);
            }
            finally
            {
                // The newer load owns the spinner
                if (!cancellationToken.IsCancellationRequested)
                    IsLoading = false;
            }
        }

        /// <summary>Opens the sheet for a new goal in the base currency (or the first one with accounts).</summary>
        [RelayCommand]
        private void AddGoal()
        {
            EditingGoal = null;
            FormName = string.Empty;
            FormAmount = string.Empty;
            HasTargetDate = false;
            TargetDate = DateTime.Today.AddYears(1);
            OpenForm(Currencies.FirstOrDefault() ?? MoneyFormatter.BaseCurrency, []);
        }

        [RelayCommand]
        private void EditGoal(GoalProgress? goal)
        {
            if (goal == null)
                return;

            EditingGoal = goal;
            FormName = goal.Name;
            FormAmount = goal.TargetAmount.ToString();
            HasTargetDate = goal.Goal.TargetDate != null;
            TargetDate = goal.Goal.TargetDate ?? DateTime.Today.AddYears(1);
            OpenForm(goal.CurrencyCode, goal.AccountIds);
        }

        private void OpenForm(string currency, IEnumerable<int> selected)
        {
            AccountChoices = [];
            _selectedIds = selected.ToHashSet();
            if (FormCurrency == currency)
                BuildAccountChoices();
            else
                FormCurrency = currency;
            IsFormOpen = true;
        }

        private void BuildAccountChoices()
        {
            foreach (var choice in AccountChoices)
            {
                if (choice.IsSelected)
                    _selectedIds.Add(choice.Account.Id);
                else
                    _selectedIds.Remove(choice.Account.Id);
            }

            var goalId = EditingGoal?.GoalId;
            AccountChoices = _accounts
                .Where(a => MoneyFormatter.SameCurrency(a.CurrencyCode, FormCurrency))
                .Where(a => !_linked.TryGetValue(a.Id, out var owner) || owner == goalId)
                .Select(a => new GoalAccountChoice(a, _selectedIds.Contains(a.Id)))
                .ToList();
        }

        [RelayCommand]
        private void CloseForm()
        {
            IsFormOpen = false;
            EditingGoal = null;
            AccountChoices = [];
        }

        /// <summary>Closes the add/edit sheet; false when it wasn't open.</summary>
        public bool CloseTopOverlay()
        {
            if (!IsFormOpen)
                return false;

            CloseForm();
            return true;
        }

        [RelayCommand]
        private async Task SaveGoalAsync()
        {
            var name = FormName.Trim();
            if (name.Length == 0)
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterGoalName);
                return;
            }

            var amount = AmountParser.Parse(FormAmount, FormCurrency);
            if (amount <= 0 || !Money.IsInRange(amount))
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterValidAmount);
                return;
            }

            var accountIds = AccountChoices.Where(c => c.IsSelected).Select(c => c.Account.Id).ToList();
            if (accountIds.Count == 0)
            {
                await _errorHandler.HandleAsync(AppResources.PleaseSelectGoalAccount);
                return;
            }

            var adding = !IsEditMode;
            var goal = new SavingsGoal
            {
                Id = EditingGoal?.GoalId ?? 0,
                Name = name,
                TargetAmount = amount,
                CurrencyCode = FormCurrency,
                TargetDate = HasTargetDate ? TargetDate.Date : null,
                CreatedDate = EditingGoal?.Goal.CreatedDate ?? DateTime.Now
            };
            try
            {
                await _goalService.SaveAsync(goal, accountIds);
                CloseForm();
                await LoadGoalsCommand.ExecuteAsync(null);
                if (adding)
                    await _dialogService.ToastAsync(AppResources.GoalAdded);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToSaveGoal);
            }
        }

        /// <summary>Deletes the goal in the sheet; the sheet stays open behind the question, so cancelling returns to it.</summary>
        [RelayCommand]
        private async Task DeleteGoalAsync()
        {
            var goal = EditingGoal;
            if (goal == null)
                return;

            var confirm = await _dialogService.ConfirmAsync(
                AppResources.ConfirmDelete,
                string.Format(AppResources.DeleteGoalConfirmation, goal.Name),
                AppResources.YesDelete,
                AppResources.Cancel);
            if (!confirm)
                return;

            try
            {
                await _goalService.DeleteAsync(goal.GoalId);
                CloseForm();
                await LoadGoalsCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToDeleteGoal);
            }
        }
    }
}
