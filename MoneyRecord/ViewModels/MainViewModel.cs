using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Views;
using System.Collections.ObjectModel;

namespace MoneyRecord.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IBalanceService _balanceService;
        private readonly ILedgerService _ledgerService;
        private readonly IBudgetSummaryService _budgetSummaryService;
        private readonly IRecurringTransactionService _recurringService;
        private readonly IErrorHandler _errorHandler;
        private readonly INavigationService _navigationService;
        private readonly ICrashReportConsent _crashReports;
        private readonly IDialogService _dialogService;
        private readonly NotificationLaunch _notificationLaunch;
        private readonly IWhatsNewService _whatsNew;
        private readonly IPrivacySettings _privacy;
        private readonly IDatabaseEncryption _databaseEncryption;
        private readonly IConversionService _conversion;
        private readonly IExchangeRateSettings _rateSettings;
        private readonly ISavingsGoalService _goals;
        private readonly ITripService _trips;
        private readonly IHomeLayoutSettings _layoutSettings;
        private readonly IGetStartedService _getStarted;
        private readonly IDebtService _debts;
        private readonly IQuickEntryService _quickEntries;
        private readonly QuickEntryRunner _quickRunner;
        private List<QuickEntryItem> _quickItems = [];
        private bool _askingCrashReports;

        // While the page is shown: a period change (here or on another tab) reloads it
        private IDisposable? _periodWatch;

        /// <summary>Home balance card: the first currency's total (base currency first), with its code when there are several.</summary>
        [ObservableProperty]
        private BalanceLine primaryBalance = new(string.Empty, null);

        /// <summary>Totals in the other currencies, one line each ("€300.00 EUR"); never added to the first.</summary>
        [ObservableProperty]
        private List<BalanceLine> otherBalances = [];

        /// <summary>Privacy mode: the balance card and the period totals show "••••••" (saved).</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowOtherTotals), nameof(ShowNoRateCaption))]
        private bool hideBalances;

        partial void OnHideBalancesChanged(bool value)
        {
            _privacy.HideBalances = value;
            OnPropertyChanged(nameof(ShowSpentLine));
            RebuildQuickChips();
        }

        /// <summary>The Home chips of the quick entries ("Coffee 3.50"; the name alone while balances are hidden).</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowQuickEntries))]
        private List<QuickEntryChip> quickChips = [];

        private void RebuildQuickChips() =>
            QuickChips = _quickItems.Select(i => new QuickEntryChip(i, HideBalances ? i.Name : i.ChipText)).ToList();

        /// <summary>One tap saves the quick entry (Undo in the toast); an entry without an amount asks for it first.</summary>
        [RelayCommand]
        private async Task RunQuickEntryAsync(QuickEntryChip? chip)
        {
            if (chip == null)
                return;

            long? cents = null;
            if (chip.Item.AsksAmount)
            {
                var typed = await _dialogService.PromptAsync(chip.Item.Name,
                    string.Format(AppResources.QuickEntryAmountPrompt, chip.Item.Name), AppResources.Save, AppResources.Cancel,
                    numeric: true);
                if (typed == null)
                    return;

                var amount = AmountParser.Parse(typed, chip.Item.CurrencyCode);
                if (amount <= 0)
                {
                    await _errorHandler.HandleAsync(AppResources.PleaseEnterValidAmount);
                    return;
                }
                cents = Money.ToCents(amount);
            }

            await _quickRunner.RunAsync(chip.Item, cents, () => LoadDataCommand.ExecuteAsync(null));
        }

        [RelayCommand]
        private void ToggleHideBalances() => HideBalances = !HideBalances;

        /// <summary>The period's totals in the base currency (the three cards).</summary>
        [ObservableProperty]
        private PeriodTotals baseTotals = new(string.Empty, 0, 0);

        partial void OnBaseTotalsChanged(PeriodTotals value)
        {
            OnPropertyChanged(nameof(SpentLineText));
            OnPropertyChanged(nameof(ShowSpentLine));
        }

        /// <summary>"October 2026: $1,770.00 spent": under the balance, so the card says how the period is going.</summary>
        public string SpentLineText => string.Format(
            LocalizationService.Instance.GetString("HomeSpentLine"), Period.Label, BaseTotals.ExpenseText);

        public bool ShowSpentLine => BaseTotals.Expense > 0 && !HideBalances;

        /// <summary>The period's totals in every other currency, one row each under the cards (never added together).</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowOtherTotals), nameof(ShowNoRateCaption))]
        private List<PeriodTotals> otherTotals = [];

        /// <summary>The other-currency rows show when there are any and balances aren't hidden.</summary>
        public bool ShowOtherTotals => OtherTotals.Count > 0 && !HideBalances;

        /// <summary>The totals are converted into the base currency (the "≈" choice of the "Show in" switch).</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowConvertedNote), nameof(ShowNoRateCaption))]
        private bool isConverted;

        /// <summary>While converted, the pills hold the amounts without a rate yet: a caption says so.</summary>
        public bool ShowNoRateCaption => IsConverted && ShowOtherTotals;

        /// <summary>"≈ USD": the converted choice of the switch.</summary>
        public string ConvertedChipText => MoneyFormatter.ApproximateMark + MoneyFormatter.BaseCurrency;

        /// <summary>The switch is offered: several currencies and some downloaded rates.</summary>
        [ObservableProperty]
        private bool canSwitchShowIn;

        /// <summary>"≈ At each day's exchange rate · updated Oct 15, 2026" while converted.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowConvertedNote))]
        private string convertedNote = string.Empty;

        public bool ShowConvertedNote => IsConverted && !string.IsNullOrEmpty(ConvertedNote);

        /// <summary>Home's "Show in" switch (the reports' "≈" chip is the same choice).</summary>
        [RelayCommand]
        private void ShowIn(string? converted)
        {
            var value = converted == bool.TrueString;
            if (value == IsConverted)
                return;

            _conversion.SetConverted(value);
            LoadDataCommand.Execute(null);
        }

        [ObservableProperty]
        private DateTime currentDate = DateTime.Now;

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowRecentEmpty))]
        private bool hasRecentTransactions;

        /// <summary>Home has loaded once; before that "nothing yet" would only be a flash.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowRecentEmpty))]
        private bool hasLoaded;

        [ObservableProperty]
        private ObservableCollection<LedgerEntry> recentTransactions = [];

        /// <summary>The next scheduled recurring transactions (the period doesn't apply).</summary>
        [ObservableProperty]
        private List<UpcomingEntry> upcomingEntries = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowUpcoming))]
        private bool hasUpcoming;

        [ObservableProperty]
        private ObservableCollection<BudgetProgress> budgetSummaries = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowBudgets))]
        private bool hasBudgets;

        [ObservableProperty]
        private int budgetsOnTrack;

        [ObservableProperty]
        private int budgetsOverBudget;

        /// <summary>The first goals (open ones by target date); the card shows once a goal exists.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasGoals), nameof(ShowGoals))]
        private List<GoalProgress> goalSummaries = [];

        public bool HasGoals => GoalSummaries.Count > 0;

        /// <summary>The active trip with its spending (Home card); null = none.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasActiveTrip), nameof(ShowTrip))]
        private TripCard? activeTrip;

        public bool HasActiveTrip => ActiveTrip != null;

        /// <summary>The first-run checklist while it is open (Home card "Get started"); null once hidden or done.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowStartChecklist))]
        private GetStartedChecklist? startChecklist;

        /// <summary>Which cards show and in what order (Settings → Home screen); read again on every load.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowPeriodPicker), nameof(ShowPeriodSummary), nameof(ShowRecent), nameof(ShowRecentEmpty), nameof(ShowStartChecklist), nameof(ShowUpcoming), nameof(ShowBudgets), nameof(ShowTrip), nameof(ShowGoals), nameof(ShowDebts), nameof(ShowQuickEntries))]
        private HomeLayout layout = HomeLayout.Default;

        // A card shows when it is enabled and has something to show
        // The period picker stays while a card that follows the period is on
        public bool ShowPeriodPicker => Layout.IsShown(HomeCard.PeriodSummary) || Layout.IsShown(HomeCard.Recent) || Layout.IsShown(HomeCard.Budgets);
        public bool ShowPeriodSummary => Layout.IsShown(HomeCard.PeriodSummary);
        // An empty period still shows the card, with a hint to add the first entry
        public bool ShowRecent => Layout.IsShown(HomeCard.Recent);
        public bool ShowRecentEmpty => HasLoaded && !HasRecentTransactions;
        public bool ShowStartChecklist => Layout.IsShown(HomeCard.GetStarted) && StartChecklist != null;
        public bool ShowUpcoming => Layout.IsShown(HomeCard.Upcoming) && HasUpcoming;
        public bool ShowBudgets => Layout.IsShown(HomeCard.Budgets) && HasBudgets;
        public bool ShowTrip => Layout.IsShown(HomeCard.Trip) && HasActiveTrip;
        public bool ShowGoals => Layout.IsShown(HomeCard.Goals) && HasGoals;
        public bool ShowDebts => Layout.IsShown(HomeCard.Debts) && HasDebts;
        public bool ShowQuickEntries => Layout.IsShown(HomeCard.QuickEntries) && QuickChips.Count > 0;

        /// <summary>The open debts, first by due date (Home card, off by default); the card shows once one is open.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasDebts), nameof(ShowDebts))]
        private List<DebtSummary> debtSummaries = [];

        public bool HasDebts => DebtSummaries.Count > 0;

        /// <summary>"Owed to you" and "You owe", per currency (never added together); empty = nothing open.</summary>
        [ObservableProperty]
        private string debtsOwedToYouText = string.Empty;

        [ObservableProperty]
        private string debtsYouOweText = string.Empty;

        /// <summary>The shared period (Home, Transactions, Budgets, Spending / Income).</summary>
        public PeriodViewModel Period { get; }

        public MainViewModel(PeriodViewModel period, IBalanceService balanceService, ILedgerService ledgerService, IBudgetSummaryService budgetSummaryService,
            IRecurringTransactionService recurringService, IErrorHandler errorHandler,
            INavigationService navigationService, ICrashReportConsent crashReports, IDialogService dialogService,
            NotificationLaunch notificationLaunch, IPrivacySettings privacy, IDatabaseEncryption databaseEncryption,
            IConversionService conversion, IExchangeRateSettings rateSettings, ISavingsGoalService goals, ITripService trips, IWhatsNewService whatsNew,
            IHomeLayoutSettings layoutSettings, IDebtService debts, IGetStartedService getStarted,
            IQuickEntryService quickEntries, QuickEntryRunner quickRunner)
        {
            _getStarted = getStarted;
            _quickEntries = quickEntries;
            _quickRunner = quickRunner;
            _debts = debts;
            _goals = goals;
            _layoutSettings = layoutSettings;
            _whatsNew = whatsNew;
            _trips = trips;
            _conversion = conversion;
            _rateSettings = rateSettings;
            _balanceService = balanceService;
            _ledgerService = ledgerService;
            _budgetSummaryService = budgetSummaryService;
            _recurringService = recurringService;
            _errorHandler = errorHandler;
            _navigationService = navigationService;
            _crashReports = crashReports;
            _dialogService = dialogService;
            _notificationLaunch = notificationLaunch;
            _privacy = privacy;
            _databaseEncryption = databaseEncryption;
            HideBalances = privacy.HideBalances;
            Period = period;

            // Home lives as long as the app: after a language change in Settings, a new value makes the bound date
            // text format again (the period pickers refresh themselves)
            LocalizationService.Instance.LanguageChanged += (_, _) => CurrentDate = DateTime.Now;
        }

        /// <summary>The page appeared: load, and follow the shared period until <see cref="Deactivate"/>.</summary>
        public async Task InitializeAsync()
        {
            _periodWatch ??= Period.WatchChanges(() => LoadDataCommand.Execute(null));
            await LoadDataCommand.ExecuteAsync(null);
            // The encrypted database's key was gone at start: say why the data is missing
            if (_databaseEncryption.TakeUnreadableDatabaseNotice())
                await _dialogService.AlertAsync(AppResources.UnreadableDatabaseTitle, AppResources.UnreadableDatabaseMessage);
            // Opened from a notification: its page, now that the Shell is ready
            await _notificationLaunch.OpenPendingAsync();
            await AskCrashReportsOnceAsync();
            // After an update: what is new in this version (once per version)
            await _whatsNew.ShowIfNewAsync();
        }

        /// <summary>Once, a week after first use: may the app send crash reports? (Only builds that can send ask.)</summary>
        private async Task AskCrashReportsOnceAsync()
        {
            if (_askingCrashReports || !_crashReports.ShouldAskNow())
                return;

            _askingCrashReports = true;
            try
            {
                var agreed = await _dialogService.ConfirmAsync(AppResources.CrashReportsQuestionTitle,
                    AppResources.CrashReportsQuestion, AppResources.CrashReportsAccept, AppResources.CrashReportsDecline);
                _crashReports.Enabled = agreed;
            }
            finally
            {
                _askingCrashReports = false;
            }
        }

        /// <summary>The page disappeared: stop following the period (it reloads when it appears again).</summary>
        public void Deactivate()
        {
            _periodWatch?.Dispose();
            _periodWatch = null;
        }

        /// <summary>
        /// Reloads Home. Each run cancels the one in progress (period changes, pull to refresh), so the
        /// results of an older period never overwrite the newer ones.
        /// </summary>
        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadDataAsync(CancellationToken cancellationToken)
        {
            try
            {
                IsRefreshing = true;

                // A hidden card skips its loads; the layout applies together with the results, so a card turned on again never shows old values
                var layout = _layoutSettings.Layout;
                Task<T> IfShown<T>(HomeCard card, Func<Task<T>> load, T whenHidden) =>
                    layout.IsShown(card) ? load() : Task.FromResult(whenHidden);

                var (startDate, endDate) = Period.Range;
                var balancesTask = _balanceService.GetAllAccountBalancesAsync();
                var totalsTask = IfShown(HomeCard.PeriodSummary, () => _conversion.GetPeriodTotalsAsync(startDate, endDate), null);
                var stateTask = IfShown(HomeCard.PeriodSummary, _conversion.GetStateAsync, null);
                var entriesTask = IfShown(HomeCard.Recent, () => _ledgerService.GetEntriesAsync(startDate, endDate), []);
                var budgetsTask = IfShown(HomeCard.Budgets, () => _budgetSummaryService.GetSummaryAsync(
                    Period.SelectedPeriod?.Type ?? PeriodType.CalendarMonth, Period.CustomStartDate, Period.CustomEndDate), []);
                var upcomingTask = IfShown(HomeCard.Upcoming, () => _recurringService.GetUpcomingAsync(), []);
                var tripTask = IfShown(HomeCard.Trip, ActiveTripAsync, null);
                async Task<TripCard?> ActiveTripAsync()
                {
                    var trip = await _trips.GetActiveAsync();
                    return trip == null ? null : new TripCard(await _trips.GetSummaryAsync(trip.Id));
                }
                // Goals reuse the balances above instead of loading them again
                var debtsTask = IfShown(HomeCard.Debts, _debts.GetAllAsync, []);
                var startTask = IfShown(HomeCard.GetStarted, _getStarted.GetOpenAsync, null);
                var goalsTask = IfShown(HomeCard.Goals, async () => await _goals.GetProgressAsync(await balancesTask), []);
                var quickTask = IfShown(HomeCard.QuickEntries, _quickEntries.GetAllAsync, []);
                await Task.WhenAll(balancesTask, totalsTask, stateTask, entriesTask, budgetsTask, upcomingTask, goalsTask, tripTask, debtsTask, startTask, quickTask);
                cancellationToken.ThrowIfCancellationRequested();

                Layout = layout;
                StartChecklist = await startTask;
                _quickItems = await quickTask;
                RebuildQuickChips();
                var balanceLines = CurrencyBalance.DisplayLines(
                    CurrencyBalance.FromAccounts((await balancesTask).Where(b => !b.IsDebt), MoneyFormatter.BaseCurrency));
                PrimaryBalance = balanceLines[0];
                OtherBalances = balanceLines.Skip(1).ToList();

                if (await totalsTask is { } totals)
                {
                    // A new record even when the values didn't change: the base currency may have (Settings)
                    BaseTotals = totals.Base;
                    OtherTotals = totals.Others;
                    IsConverted = totals.IsConverted;
                    // Offered with several currencies and some rates (the service's one rule)
                    CanSwitchShowIn = (await stateTask)!.CanConvert;
                    OnPropertyChanged(nameof(ConvertedChipText));
                    ConvertedNote = _rateSettings.LastUpdated is { } updated
                        ? string.Format(AppResources.ConvertedAtDailyRate, DateFormatter.Format(updated, DateStyle.Medium))
                        : string.Empty;
                }

                // Whole transfers: the Recent card lists a transfer once
                RecentTransactions = new((await entriesTask).OrderByDescending(e => e.Date).Take(5));
                HasRecentTransactions = RecentTransactions.Count > 0;
                HasLoaded = true;

                // Entries waiting for their amount come first
                UpcomingEntries = (await upcomingTask).OrderByDescending(e => e.CanEnter).Take(3).ToList();
                HasUpcoming = UpcomingEntries.Count > 0;

                // The budget card uses the Home period, like the Budget page does with its own
                var budgets = await budgetsTask;
                BudgetsOnTrack = budgets.Count(b => !b.IsOverBudget);
                BudgetsOverBudget = budgets.Count(b => b.IsOverBudget);
                BudgetSummaries = new(budgets.Take(3));
                HasBudgets = budgets.Count > 0;

                // Goals don't follow the period: saved is today's balance
                GoalSummaries = (await goalsTask).Take(3).ToList();
                ActiveTrip = await tripTask;

                var openDebts = (await debtsTask).Where(d => !d.IsPaidOff).ToList();
                DebtsOwedToYouText = OrDash(DebtSummary.OutstandingTotals(openDebts, DebtDirection.Lent));
                DebtsYouOweText = OrDash(DebtSummary.OutstandingTotals(openDebts, DebtDirection.Borrowed));
                DebtSummaries = openDebts.Take(3).ToList();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A newer load replaced this one
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadTransactions);
            }
            finally
            {
                // The newer load owns the spinner
                if (!cancellationToken.IsCancellationRequested)
                    IsRefreshing = false;
            }
        }

        /// <summary>The balance card opens all accounts; a currency line opens the accounts in that currency.</summary>
        [RelayCommand]
        private Task OpenAccountsAsync(BalanceLine? line) => line?.CurrencyCode is { } code
            ? _navigationService.NavigateToAsync($"{nameof(ManageAccountsPage)}?CurrencyCode={code}")
            : _navigationService.NavigateToAsync(nameof(ManageAccountsPage));

        [RelayCommand]
        private Task SeeAllTransactionsAsync() => _navigationService.NavigateToAsync($"//{nameof(TransactionsPage)}");

        /// <summary>A due variable-amount row opens the add form on that occurrence; other rows do nothing.</summary>
        [RelayCommand]
        private Task EnterOccurrenceAsync(UpcomingEntry? entry) => entry is { CanEnter: true }
            ? _navigationService.NavigateToAsync(ReminderService.PendingRoute(entry.RecurringId, entry.Date))
            : Task.CompletedTask;

        [RelayCommand]
        private Task SeeAllUpcomingAsync() =>
            _navigationService.NavigateToAsync($"{nameof(RecurringTransactionsPage)}?Show={RecurringTransactionsViewModel.ShowUpcoming}");

        [RelayCommand]
        private Task ViewAllBudgetsAsync() => _navigationService.NavigateToAsync($"//{nameof(BudgetPage)}");

        [RelayCommand]
        private Task ViewAllGoalsAsync() => _navigationService.NavigateToAsync(nameof(GoalsPage));

        [RelayCommand]
        private Task CustomizeHomeAsync() => _navigationService.NavigateToAsync(nameof(HomeLayoutPage));

        [RelayCommand]
        private Task OpenStartStepAsync(GetStartedRow? row) => row?.Step switch
        {
            null => Task.CompletedTask,
            GetStartedStep.AddEntry => _navigationService.NavigateToAsync(nameof(AddTransactionPage),
                new Dictionary<string, object> { { "TransactionType", TransactionType.Expense } }),
            // Backup, app lock and reminders are all Settings cards
            _ => _navigationService.NavigateToAsync(nameof(SettingsPage))
        };

        [RelayCommand]
        private void HideStartChecklist()
        {
            _getStarted.Dismiss();
            StartChecklist = null;
        }

        private static string OrDash(string text) => text.Length > 0 ? text : "—";

        [RelayCommand]
        private Task OpenQuickEntriesAsync() => _navigationService.NavigateToAsync(nameof(QuickEntriesPage));

        [RelayCommand]
        private Task OpenDebtsAsync() => _navigationService.NavigateToAsync(nameof(DebtsPage));

        [RelayCommand]
        private Task OpenTripAsync() => ActiveTrip == null
            ? Task.CompletedTask
            : _navigationService.NavigateToAsync(nameof(TripPage), new Dictionary<string, object> { ["TripId"] = ActiveTrip.TripId.ToString() });
    }
}
