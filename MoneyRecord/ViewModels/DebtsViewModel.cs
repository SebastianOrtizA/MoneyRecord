using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>An account choice in the debt sheets; no account = owed already (nothing moves).</summary>
    public sealed record DebtAccountOption(Account? Account, string Name)
    {
        public override string ToString() => Name;
    }

    /// <summary>
    /// Debts page: open / paid-off cards with what each person owes or is owed, the detail sheet (history, payments), the
    /// add / edit sheet (with the optional payment schedule) and the payment sheet (a payment, or more lent / borrowed).
    /// </summary>
    public partial class DebtsViewModel : ObservableObject
    {
        private readonly IDebtService _debtService;
        private readonly IPayeeService _payeeService;
        private readonly IAccountRepository _accountRepository;
        private readonly IReminderService _reminders;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;

        private List<DebtSummary> _all = [];
        private List<Account> _accounts = [];
        private List<PayeeSummary> _payees = [];

        public DebtsViewModel(IDebtService debtService, IPayeeService payeeService, IAccountRepository accountRepository,
            IReminderService reminders, IErrorHandler errorHandler, IDialogService dialogService)
        {
            _debtService = debtService;
            _payeeService = payeeService;
            _accountRepository = accountRepository;
            _reminders = reminders;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
        }

        public List<RecurrenceFrequencyItem> Frequencies { get; } =
            Enum.GetValues<RecurrenceFrequency>()
                .Select(f => new RecurrenceFrequencyItem { Frequency = f, Display = DebtSummary.FrequencyName(f) })
                .ToList();

        // ---- List ----

        [ObservableProperty]
        private bool isLoading;

        /// <summary>The page lists the paid-off debts instead of the open ones.</summary>
        [ObservableProperty]
        private bool showPaidOff;

        partial void OnShowPaidOffChanged(bool value) => ApplyFilter();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasDebts))]
        private List<DebtSummary> debts = [];

        public bool HasDebts => Debts.Count > 0;

        public string EmptyTitle => ShowPaidOff ? AppResources.NoPaidOffDebts : AppResources.NoDebtsYet;

        /// <summary>What the people owe the user, open debts only, one amount per currency.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasOwedToYou))]
        private string owedToYouText = string.Empty;

        public bool HasOwedToYou => OwedToYouText.Length > 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasYouOwe))]
        private string youOweText = string.Empty;

        public bool HasYouOwe => YouOweText.Length > 0;

        public Task InitializeAsync() => LoadDebtsCommand.ExecuteAsync(null);

        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadDebtsAsync(CancellationToken cancellationToken)
        {
            try
            {
                IsLoading = true;

                var debtsTask = _debtService.GetAllAsync();
                var accountsTask = _accountRepository.GetOwnAsync();
                var payeesTask = _payeeService.GetAllAsync();
                await Task.WhenAll(debtsTask, accountsTask, payeesTask);
                cancellationToken.ThrowIfCancellationRequested();

                _accounts = (await accountsTask).OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
                _payees = await payeesTask;
                _all = await debtsTask;
                OwedToYouText = Totals(DebtDirection.Lent);
                YouOweText = Totals(DebtDirection.Borrowed);
                ApplyFilter();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A newer load replaced this one
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadDebts);
            }
            finally
            {
                // The newer load owns the spinner
                if (!cancellationToken.IsCancellationRequested)
                    IsLoading = false;
            }
        }

        // Never added across currencies: "$120.00 · €40.00"
        private string Totals(DebtDirection direction) => string.Join(" · ", _all
            .Where(d => d.Direction == direction && !d.IsPaidOff)
            .GroupBy(d => d.CurrencyCode)
            .OrderBy(g => g.Key != MoneyFormatter.BaseCurrency)
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => MoneyFormatter.Format(Money.FromCents(g.Sum(d => d.OutstandingCents)), g.Key)));

        private void ApplyFilter()
        {
            Debts = _all.Where(d => d.IsPaidOff == ShowPaidOff).ToList();
            OnPropertyChanged(nameof(EmptyTitle));
        }

        [RelayCommand]
        private void SelectOpen() => ShowPaidOff = false;

        [RelayCommand]
        private void SelectPaidOff() => ShowPaidOff = true;

        private List<Account> AccountsIn(string currency) =>
            _accounts.Where(a => MoneyFormatter.SameCurrency(a.CurrencyCode, currency)).ToList();

        // ---- Detail sheet ----

        [ObservableProperty]
        private bool isDetailOpen;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AddMoreText))]
        private DebtSummary? selectedDebt;

        [ObservableProperty]
        private List<DebtMovement> movements = [];

        public string AddMoreText => SelectedDebt?.Direction == DebtDirection.Borrowed ? AppResources.DebtBorrowMore : AppResources.DebtLendMore;

        [RelayCommand]
        private async Task OpenDebtAsync(DebtSummary? debt)
        {
            if (debt == null)
                return;

            try
            {
                Movements = await _debtService.GetMovementsAsync(debt.DebtId);
                SelectedDebt = debt;
                IsDetailOpen = true;
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadDebts);
            }
        }

        [RelayCommand]
        private void CloseDetail() => IsDetailOpen = false;

        // After a change: the list again, and the debt's sheet with its new amounts. Read on their own: the RefreshView
        // starts a second load when the first one sets IsLoading, which cancels the first, so the list may not be
        // updated yet when the awaited load returns
        private async Task ReloadAndReopenAsync(int debtId)
        {
            await LoadDebtsCommand.ExecuteAsync(null);
            await OpenDebtAsync((await _debtService.GetAllAsync()).FirstOrDefault(d => d.DebtId == debtId));
        }

        [RelayCommand]
        private async Task DeleteMovementAsync(DebtMovement? movement)
        {
            var debt = SelectedDebt;
            if (movement is not { CanDelete: true } || debt == null)
                return;

            if (!await _dialogService.ConfirmAsync(AppResources.ConfirmDelete,
                    string.Format(AppResources.DeleteDebtMovementConfirmation, movement.KindText, movement.AmountText, movement.DetailText),
                    AppResources.YesDelete, AppResources.Cancel))
                return;

            try
            {
                await _debtService.DeleteMovementAsync(movement);
                await ReloadAndReopenAsync(debt.DebtId);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToSaveDebt);
            }
        }

        // ---- Add / edit sheet ----

        [ObservableProperty]
        private bool isFormOpen;

        /// <summary>The debt the sheet edits; null when it adds one.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEditMode), nameof(FormTitle), nameof(SaveText))]
        private DebtSummary? editingDebt;

        public bool IsEditMode => EditingDebt != null;

        public string FormTitle => IsEditMode ? AppResources.EditDebt : AppResources.AddDebt;

        public string SaveText => IsEditMode ? AppResources.Save : AppResources.AddDebt;

        /// <summary>I lent (true) or I borrowed (false); only when adding.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AmountCaption), nameof(AccountCaption), nameof(ScheduleAccountCaption))]
        private bool formIsLent = true;

        partial void OnFormIsLentChanged(bool value) => BuildFormAccounts();

        public string AmountCaption => FormIsLent ? AppResources.DebtAmountLent : AppResources.DebtAmountBorrowed;

        public string AccountCaption => FormIsLent ? AppResources.DebtFromAccount : AppResources.DebtIntoAccount;

        public string ScheduleAccountCaption => FormIsLent ? AppResources.DebtPaidIntoAccount : AppResources.DebtPaidFromAccount;

        [ObservableProperty]
        private string formPerson = string.Empty;

        partial void OnFormPersonChanged(string value)
        {
            // No suggestion once the name is a known payee's
            PersonSuggestions = PayeeNames.Find(_payees, value) != null ? [] : PayeeNames.Suggest(_payees, value);
        }

        [ObservableProperty]
        private List<PayeeSummary> personSuggestions = [];

        [RelayCommand]
        private void SelectPerson(PayeeSummary payee)
        {
            FormPerson = payee.Name;
            PersonSuggestions = [];
        }

        [ObservableProperty]
        private string formAmount = string.Empty;

        [ObservableProperty]
        private string formCurrency = string.Empty;

        partial void OnFormCurrencyChanged(string value) => BuildFormAccounts();

        /// <summary>The currencies of the accounts, base currency first.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowCurrencyPicker))]
        private List<string> currencies = [];

        public bool ShowCurrencyPicker => !IsEditMode && Currencies.Count > 1;

        /// <summary>"Already owed" first, then the accounts in the debt's currency.</summary>
        [ObservableProperty]
        private List<DebtAccountOption> formAccounts = [];

        [ObservableProperty]
        private DebtAccountOption? formAccount;

        [ObservableProperty]
        private DateTime formDate = DateTime.Today;

        [ObservableProperty]
        private string formNote = string.Empty;

        [ObservableProperty]
        private bool hasDueDate;

        [ObservableProperty]
        private DateTime dueDate = DateTime.Today.AddMonths(1);

        // The schedule: payments every period from the first date

        [ObservableProperty]
        private bool hasSchedule;

        [ObservableProperty]
        private string scheduleAmount = string.Empty;

        [ObservableProperty]
        private RecurrenceFrequencyItem? scheduleFrequency;

        [ObservableProperty]
        private DateTime scheduleFirstDate = DateTime.Today.AddMonths(1);

        [ObservableProperty]
        private List<Account> scheduleAccounts = [];

        [ObservableProperty]
        private Account? scheduleAccount;

        [ObservableProperty]
        private bool hasScheduleInterest;

        [ObservableProperty]
        private string scheduleInterest = string.Empty;

        [RelayCommand]
        private void AddDebt()
        {
            EditingDebt = null;
            FormIsLent = true;
            FormPerson = string.Empty;
            FormAmount = string.Empty;
            FormDate = DateTime.Today;
            FormNote = string.Empty;
            HasDueDate = false;
            DueDate = DateTime.Today.AddMonths(1);
            ResetSchedule(null);
            Currencies = _accounts
                .Select(a => MoneyFormatter.ResolveCurrencyCode(a.CurrencyCode))
                .Append(MoneyFormatter.BaseCurrency)
                .Distinct()
                .OrderBy(code => code != MoneyFormatter.BaseCurrency)
                .ThenBy(code => code, StringComparer.Ordinal)
                .ToList();
            SetFormCurrency(MoneyFormatter.BaseCurrency);
            PersonSuggestions = [];
            IsFormOpen = true;
        }

        [RelayCommand]
        private void EditDebt()
        {
            var debt = SelectedDebt;
            if (debt == null)
                return;

            IsDetailOpen = false;
            EditingDebt = debt;
            FormIsLent = debt.Direction == DebtDirection.Lent;
            FormPerson = debt.PersonName;
            FormNote = debt.Debt.Note;
            HasDueDate = debt.Debt.DueDate != null;
            DueDate = debt.Debt.DueDate ?? DateTime.Today.AddMonths(1);
            Currencies = [debt.CurrencyCode];
            SetFormCurrency(debt.CurrencyCode);
            ResetSchedule(debt.Schedule);
            OnPropertyChanged(nameof(ShowCurrencyPicker));
            PersonSuggestions = [];
            IsFormOpen = true;
        }

        private void SetFormCurrency(string currency)
        {
            if (FormCurrency == currency)
                BuildFormAccounts();
            else
                FormCurrency = currency;
        }

        private void BuildFormAccounts()
        {
            var accounts = AccountsIn(FormCurrency);
            FormAccounts = accounts.Select(a => new DebtAccountOption(a, a.Name))
                .Prepend(new DebtAccountOption(null, AppResources.DebtAlreadyOwedOption))
                .ToList();
            // Money usually moves: the first account rather than "Already owed"
            FormAccount = FormAccounts.Count > 1 ? FormAccounts[1] : FormAccounts[0];

            var scheduled = ScheduleAccount?.Id;
            ScheduleAccounts = accounts;
            ScheduleAccount = accounts.FirstOrDefault(a => a.Id == scheduled) ?? accounts.FirstOrDefault();
        }

        private void ResetSchedule(RecurringTransaction? schedule)
        {
            HasSchedule = schedule != null;
            ScheduleAmount = schedule?.Amount.ToString() ?? string.Empty;
            ScheduleFrequency = Frequencies.First(f => f.Frequency == (schedule?.Frequency ?? RecurrenceFrequency.Monthly));
            ScheduleFirstDate = schedule?.NextOccurrence.Date ?? DateTime.Today.AddMonths(1);
            HasScheduleInterest = schedule is { InterestCents: > 0 };
            ScheduleInterest = schedule is { InterestCents: > 0 } ? schedule.InterestAmount.ToString() : string.Empty;
            ScheduleAccount = schedule == null ? null : _accounts.FirstOrDefault(a => a.Id == schedule.AccountId);
            if (schedule != null)
                ScheduleAccounts = AccountsIn(FormCurrency);
        }

        [RelayCommand]
        private void CloseForm()
        {
            IsFormOpen = false;
            EditingDebt = null;
        }

        [RelayCommand]
        private async Task SaveDebtAsync()
        {
            var person = PayeeNames.Normalize(FormPerson);
            if (person.Length == 0)
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterDebtPerson);
                return;
            }

            decimal amount = 0;
            if (!IsEditMode)
            {
                amount = AmountParser.Parse(FormAmount, FormCurrency);
                if (amount <= 0 || !Money.IsInRange(amount))
                {
                    await _errorHandler.HandleAsync(AppResources.PleaseEnterValidAmount);
                    return;
                }
            }

            DebtScheduleDraft? schedule = null;
            if (HasSchedule)
            {
                var payment = AmountParser.Parse(ScheduleAmount, FormCurrency);
                var interest = HasScheduleInterest ? AmountParser.Parse(ScheduleInterest, FormCurrency) : 0;
                if (payment <= 0 || !Money.IsInRange(payment) || interest < 0 || !Money.IsInRange(interest))
                {
                    await _errorHandler.HandleAsync(AppResources.PleaseEnterValidAmount);
                    return;
                }
                if (ScheduleAccount == null)
                {
                    await _errorHandler.HandleAsync(AppResources.PleaseSelectAccount);
                    return;
                }
                schedule = new DebtScheduleDraft(payment, interest, ScheduleFrequency?.Frequency ?? RecurrenceFrequency.Monthly,
                    ScheduleFirstDate.Date, ScheduleAccount.Id);
            }

            var editing = EditingDebt;
            try
            {
                int debtId;
                if (editing != null)
                {
                    await _debtService.UpdateAsync(editing.DebtId, person, FormNote, HasDueDate ? DueDate : null, schedule);
                    debtId = editing.DebtId;
                }
                else
                {
                    debtId = await _debtService.AddAsync(new DebtDraft(FormIsLent ? DebtDirection.Lent : DebtDirection.Borrowed,
                        person, FormCurrency, amount, FormAccount?.Account?.Id, FormDate.Date + DateTime.Now.TimeOfDay, FormNote,
                        HasDueDate ? DueDate : null), schedule);
                }

                CloseForm();
                ShowPaidOff = false;
                if (editing == null)
                {
                    await LoadDebtsCommand.ExecuteAsync(null);
                    await _dialogService.ToastAsync(AppResources.DebtAdded);
                }
                else
                {
                    await ReloadAndReopenAsync(debtId);
                }

                // A schedule or due date is something to be reminded of
                if (schedule != null || HasDueDate)
                    await _reminders.OfferAsync();
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToSaveDebt);
            }
        }

        /// <summary>Deletes the debt in the sheet with its history; the sheet stays open behind the question.</summary>
        [RelayCommand]
        private async Task DeleteDebtAsync()
        {
            var debt = EditingDebt;
            if (debt == null)
                return;

            if (!await _dialogService.ConfirmAsync(AppResources.ConfirmDelete,
                    string.Format(AppResources.DeleteDebtConfirmation, debt.PersonName),
                    AppResources.YesDelete, AppResources.Cancel))
                return;

            try
            {
                await _debtService.DeleteAsync(debt.DebtId);
                CloseForm();
                await LoadDebtsCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToDeleteDebt);
            }
        }

        // ---- Payment sheet: a payment, or more lent / borrowed ----

        [ObservableProperty]
        private bool isPaymentOpen;

        /// <summary>More lent or borrowed instead of a payment.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PaymentTitle), nameof(PaymentAccountCaption))]
        private bool paymentIsAddition;

        public string PaymentTitle => PaymentIsAddition ? AddMoreText : AppResources.DebtRecordPayment;

        public string PaymentAccountCaption => (SelectedDebt?.Direction == DebtDirection.Lent) != PaymentIsAddition
            ? AppResources.DebtPaidIntoAccount
            : AppResources.DebtPaidFromAccount;

        [ObservableProperty]
        private string paymentAmount = string.Empty;

        [ObservableProperty]
        private List<Account> paymentAccounts = [];

        [ObservableProperty]
        private Account? paymentAccount;

        [ObservableProperty]
        private DateTime paymentDate = DateTime.Today;

        [ObservableProperty]
        private bool hasPaymentInterest;

        [ObservableProperty]
        private string paymentInterest = string.Empty;

        /// <summary>"Left: $60.00" under a payment's amount.</summary>
        public string PaymentHint => SelectedDebt is { } debt && !PaymentIsAddition
            ? string.Format(AppResources.DebtLeft, debt.OutstandingText)
            : string.Empty;

        [RelayCommand]
        private Task RecordPaymentAsync() => OpenPaymentAsync(addition: false);

        [RelayCommand]
        private Task AddMoreAsync() => OpenPaymentAsync(addition: true);

        private async Task OpenPaymentAsync(bool addition)
        {
            var debt = SelectedDebt;
            if (debt == null)
                return;

            var accounts = AccountsIn(debt.CurrencyCode);
            if (accounts.Count == 0)
            {
                await _errorHandler.HandleAsync(string.Format(AppResources.DebtNoAccountInCurrency, debt.CurrencyCode));
                return;
            }

            IsDetailOpen = false;
            PaymentIsAddition = addition;
            // A payment starts at the scheduled amount, or what is left
            PaymentAmount = addition ? string.Empty
                : Money.FromCents(Math.Min(debt.Schedule?.AmountCents ?? long.MaxValue, debt.OutstandingCents)).ToString();
            PaymentAccounts = accounts;
            PaymentAccount = accounts.FirstOrDefault(a => a.Id == debt.Schedule?.AccountId) ?? accounts[0];
            PaymentDate = DateTime.Today;
            HasPaymentInterest = false;
            PaymentInterest = string.Empty;
            OnPropertyChanged(nameof(PaymentHint));
            OnPropertyChanged(nameof(PaymentAccountCaption));
            IsPaymentOpen = true;
        }

        [RelayCommand]
        private void ClosePayment() => IsPaymentOpen = false;

        [RelayCommand]
        private async Task SavePaymentAsync()
        {
            var debt = SelectedDebt;
            if (debt == null || PaymentAccount == null)
                return;

            var amount = AmountParser.Parse(PaymentAmount, debt.CurrencyCode);
            var interest = HasPaymentInterest && !PaymentIsAddition ? AmountParser.Parse(PaymentInterest, debt.CurrencyCode) : 0;
            if (amount <= 0 || !Money.IsInRange(amount) || interest < 0 || !Money.IsInRange(interest))
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterValidAmount);
                return;
            }
            if (!PaymentIsAddition && Money.ToCents(amount) > debt.OutstandingCents)
            {
                await _errorHandler.HandleAsync(string.Format(AppResources.DebtPaymentTooLarge, debt.OutstandingText));
                return;
            }

            try
            {
                var date = PaymentDate.Date + DateTime.Now.TimeOfDay;
                var paidOff = false;
                if (PaymentIsAddition)
                    await _debtService.AddToDebtAsync(debt.DebtId, PaymentAccount.Id, date, amount);
                else
                    paidOff = await _debtService.RecordPaymentAsync(debt.DebtId, PaymentAccount.Id, date, amount, interest);

                IsPaymentOpen = false;
                if (paidOff)
                {
                    await LoadDebtsCommand.ExecuteAsync(null);
                    await _dialogService.ToastAsync(string.Format(AppResources.DebtPaidOffToast, debt.PersonName));
                }
                else
                {
                    await ReloadAndReopenAsync(debt.DebtId);
                }
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToSaveDebt);
            }
        }

        /// <summary>Closes the top sheet (payment, then add / edit, then detail); false when none was open.</summary>
        public bool CloseTopOverlay()
        {
            if (IsPaymentOpen)
                ClosePayment();
            else if (IsFormOpen)
                CloseForm();
            else if (IsDetailOpen)
                CloseDetail();
            else
                return false;
            return true;
        }

        /// <summary>The page went away: back here later shows the list, not a stale sheet.</summary>
        public void CloseAll()
        {
            IsPaymentOpen = false;
            CloseForm();
            IsDetailOpen = false;
        }
    }
}
