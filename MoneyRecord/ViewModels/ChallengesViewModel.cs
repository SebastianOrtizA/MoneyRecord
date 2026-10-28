using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>A picker item: the text is what the picker shows.</summary>
    public sealed record ChallengeModeOption(ChallengeMode Mode, string Label)
    {
        public override string ToString() => Label;
    }

    public sealed record ChallengeRoundingOption(long Cents, string Label)
    {
        public override string ToString() => Label;
    }

    /// <summary>A destination to pick: an account or a pocket; a null <see cref="Place"/> is "none".</summary>
    public sealed record ChallengePlaceChoice(ChallengePlace? Place, string Label)
    {
        public override string ToString() => Label;
    }

    /// <summary>Challenges page: one card per challenge (a tap opens the edit sheet) and the add/edit sheet with a live preview.</summary>
    public partial class ChallengesViewModel : ObservableObject
    {
        private const int MaxDays = 3650;

        private readonly ISavingsChallengeService _challengeService;
        private readonly IAccountRepository _accountRepository;
        private readonly IPocketService _pocketService;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;
        private readonly INavigationService _navigation;

        private List<Account> _accounts = [];
        private List<PocketSummary> _pockets = [];
        // The destination the sheet is opened with (kept across the currency rebuild)
        private ChallengePlace? _wantedDestination;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasChallenges))]
        private List<ChallengeSummary> challenges = [];

        public bool HasChallenges => Challenges.Count > 0;

        /// <summary>The add/edit sheet is open.</summary>
        [ObservableProperty]
        private bool isFormOpen;

        /// <summary>The challenge the sheet edits; null when it adds one.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEditMode), nameof(FormTitle), nameof(SaveText), nameof(IsPlanEditable), nameof(IsPlanLocked), nameof(IsDaysEditable))]
        private ChallengeSummary? editing;

        public bool IsEditMode => Editing != null;

        public string FormTitle => IsEditMode ? AppResources.EditChallenge : AppResources.NewChallenge;

        public string SaveText => IsEditMode ? AppResources.Save : AppResources.AddChallenge;

        /// <summary>Goal, days, mode, rounding and currency are fixed once a day is done.</summary>
        public bool IsPlanEditable => Editing is not { DoneCount: > 0 };

        public bool IsPlanLocked => !IsPlanEditable;

        public string PlanLockedText => AppResources.ChallengePlanLocked;

        [ObservableProperty]
        private string formName = string.Empty;

        [ObservableProperty]
        private string formGoal = string.Empty;

        partial void OnFormGoalChanged(string value) => UpdatePreview();

        [ObservableProperty]
        private string formDays = "30";

        partial void OnFormDaysChanged(string value)
        {
            OnPropertyChanged(nameof(DaysEntryText));
            UpdatePreview();
        }

        /// <summary>The days a typed daily amount needs to reach the goal; empty while that can't be worked out.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DaysEntryText))]
        private string derivedDays = string.Empty;

        /// <summary>What the days entry shows: the typed days, or (read-only) the days the daily amount needs.</summary>
        public string DaysEntryText
        {
            get => UsesDayAmount ? DerivedDays : FormDays;
            set
            {
                if (!UsesDayAmount)
                    FormDays = value;
            }
        }

        /// <summary>Equal mode: the fixed amount a day; empty = the goal split over the days.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(UsesDayAmount), nameof(IsDaysEditable), nameof(ShowRounding), nameof(DaysEntryText))]
        private string formDayAmount = string.Empty;

        partial void OnFormDayAmountChanged(string value) => UpdatePreview();

        /// <summary>Linear mode: what each day adds; empty = worked out from the days.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(UsesDayAmount), nameof(IsDaysEditable), nameof(ShowRounding), nameof(DaysEntryText))]
        private string formStep = string.Empty;

        partial void OnFormStepChanged(string value) => UpdatePreview();

        /// <summary>Linear mode: what day 1 saves; empty = the same as the step.</summary>
        [ObservableProperty]
        private string formFirstDay = string.Empty;

        partial void OnFormFirstDayChanged(string value) => UpdatePreview();

        // The picker can clear its selection for a moment while its items change
        public bool IsEqualMode => SelectedMode?.Mode == ChallengeMode.Equal;

        public bool IsLinearMode => SelectedMode?.Mode == ChallengeMode.Linear;

        /// <summary>The typed daily amount of the mode: the Equal amount or the Linear step.</summary>
        private string DayAmountText => IsLinearMode ? FormStep : IsEqualMode ? FormDayAmount : string.Empty;

        /// <summary>A daily amount (or step) is typed: the days follow from the goal.</summary>
        public bool UsesDayAmount => !string.IsNullOrWhiteSpace(DayAmountText);

        public bool IsDaysEditable => IsPlanEditable && !UsesDayAmount;

        public bool ShowRounding => !UsesDayAmount;

        [ObservableProperty]
        private DateTime formStartDate = DateTime.Today;

        [ObservableProperty]
        private string formCurrency = string.Empty;

        partial void OnFormCurrencyChanged(string value)
        {
            BuildDestinationChoices();
            UpdatePreview();
        }

        /// <summary>The currencies of the accounts, base currency first.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowCurrencyPicker))]
        private List<string> currencies = [];

        public bool ShowCurrencyPicker => Currencies.Count > 1;

        // Doubling is in the list only while editing a challenge that already uses it; it is added and removed in place,
        // so the picker keeps its selection (swapping the whole list made it clear it)
        public ObservableCollection<ChallengeModeOption> Modes { get; } = new(BuildModes(false));

        public IReadOnlyList<ChallengeRoundingOption> Roundings { get; private set; } = BuildRoundings();

        [ObservableProperty]
        private ChallengeModeOption selectedMode;

        partial void OnSelectedModeChanged(ChallengeModeOption value)
        {
            OnPropertyChanged(nameof(IsEqualMode));
            OnPropertyChanged(nameof(IsLinearMode));
            OnPropertyChanged(nameof(UsesDayAmount));
            OnPropertyChanged(nameof(IsDaysEditable));
            OnPropertyChanged(nameof(ShowRounding));
            OnPropertyChanged(nameof(DaysEntryText));
            UpdatePreview();
        }

        [ObservableProperty]
        private ChallengeRoundingOption selectedRounding;

        partial void OnSelectedRoundingChanged(ChallengeRoundingOption value) => UpdatePreview();

        /// <summary>"None", then the accounts and pockets in the challenge's currency.</summary>
        [ObservableProperty]
        private List<ChallengePlaceChoice> destinationOptions = [];

        /// <summary>Where the saved money goes; each day asks where it comes from.</summary>
        [ObservableProperty]
        private ChallengePlaceChoice? destination;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasPreview), nameof(ShowPreviewCard))]
        private string previewDaysText = string.Empty;

        [ObservableProperty]
        private string previewTotalText = string.Empty;

        /// <summary>What is wrong with the plan: a doubling that runs in levels, or a first day that is too high.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasPlanWarning), nameof(ShowPreviewCard))]
        private string planWarningText = string.Empty;

        public bool HasPreview => PreviewDaysText.Length > 0;

        public bool HasPlanWarning => PlanWarningText.Length > 0;

        public bool ShowPreviewCard => HasPreview || HasPlanWarning;

        public ChallengesViewModel(ISavingsChallengeService challengeService, IAccountRepository accountRepository, IPocketService pocketService, INavigationService navigation, IErrorHandler errorHandler,
            IDialogService dialogService)
        {
            _challengeService = challengeService;
            _navigation = navigation;
            _accountRepository = accountRepository;
            _pocketService = pocketService;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
            selectedMode = Modes[0];
            selectedRounding = Roundings[0];
        }

        private static List<ChallengeModeOption> BuildModes(bool withDoubling)
        {
            List<ChallengeModeOption> modes =
            [
                new(ChallengeMode.Linear, AppResources.ChallengeModeLinear),
                new(ChallengeMode.Equal, AppResources.ChallengeModeEqual)
            ];
            if (withDoubling)
                modes.Add(new(ChallengeMode.Doubling, AppResources.ChallengeModeDoubling));
            return modes;
        }

        /// <summary>Offers Doubling (or stops offering it) in the mode picker. Select another mode before taking it away.</summary>
        private void OfferDoubling(bool offer)
        {
            var doubling = Modes.FirstOrDefault(m => m.Mode == ChallengeMode.Doubling);
            if (offer && doubling == null)
                Modes.Add(new(ChallengeMode.Doubling, AppResources.ChallengeModeDoubling));
            else if (!offer && doubling != null)
                Modes.Remove(doubling);
        }

        private static List<ChallengeRoundingOption> BuildRoundings() =>
        [
            new(0, AppResources.ChallengeRoundingNone),
            .. new[] { 1, 5, 10, 50, 100, 500, 1000 }.Select(units =>
                new ChallengeRoundingOption(units * 100L, string.Format(AppResources.ChallengeRoundTo, units.ToString("N0"))))
        ];

        private void RebuildLabels()
        {
            var mode = SelectedMode.Mode;
            var rounding = SelectedRounding.Cents;
            var withDoubling = Modes.Any(m => m.Mode == ChallengeMode.Doubling);
            Modes.Clear();
            foreach (var option in BuildModes(withDoubling))
                Modes.Add(option);
            Roundings = BuildRoundings();
            OnPropertyChanged(nameof(Roundings));
            SelectedMode = Modes.First(m => m.Mode == mode);
            SelectedRounding = Roundings.First(r => r.Cents == rounding);
        }

        /// <summary>The picker labels follow a language change while the page is shown (the view model is transient).</summary>
        public void WatchLanguage()
        {
            UnwatchLanguage();
            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        }

        public void UnwatchLanguage() => LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;

        private void OnLanguageChanged(object? sender, EventArgs e) => RebuildLabels();

        public Task InitializeAsync() => LoadChallengesCommand.ExecuteAsync(null);

        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadChallengesAsync(CancellationToken cancellationToken)
        {
            try
            {
                IsLoading = true;

                var challengesTask = _challengeService.GetSummariesAsync();
                var accountsTask = _accountRepository.GetOwnAsync();
                var pocketsTask = _pocketService.GetPocketsAsync();
                await Task.WhenAll(challengesTask, accountsTask, pocketsTask);
                cancellationToken.ThrowIfCancellationRequested();

                _pockets = await pocketsTask;
                _accounts = (await accountsTask).OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
                var baseCode = MoneyFormatter.BaseCurrency;
                Currencies = _accounts
                    .Select(a => MoneyFormatter.ResolveCurrencyCode(a.CurrencyCode))
                    .Distinct()
                    .OrderBy(code => code != baseCode)
                    .ThenBy(code => code, StringComparer.Ordinal)
                    .ToList();
                Challenges = await challengesTask;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A newer load replaced this one
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadChallenges);
            }
            finally
            {
                // The newer load owns the spinner
                if (!cancellationToken.IsCancellationRequested)
                    IsLoading = false;
            }
        }

        /// <summary>Opens the challenge's game page.</summary>
        [RelayCommand]
        private Task OpenChallengeAsync(ChallengeSummary? summary) =>
            summary == null
                ? Task.CompletedTask
                : _navigation.NavigateToAsync(nameof(Views.ChallengePage), new Dictionary<string, object> { ["ChallengeId"] = summary.Challenge.Id.ToString() });

        [RelayCommand]
        private void AddChallenge()
        {
            Editing = null;
            FormName = string.Empty;
            FormGoal = string.Empty;
            FormDays = "30";
            FormStartDate = DateTime.Today;
            FormDayAmount = string.Empty;
            FormStep = string.Empty;
            FormFirstDay = string.Empty;
            SelectedMode = Modes[0];
            OfferDoubling(false);
            SelectedRounding = Roundings[0];
            OpenForm(Currencies.FirstOrDefault() ?? MoneyFormatter.BaseCurrency, null);
        }

        [RelayCommand]
        private void EditChallenge(ChallengeSummary? summary)
        {
            if (summary == null)
                return;

            var challenge = summary.Challenge;
            Editing = summary;
            FormName = challenge.Name;
            FormGoal = challenge.Goal.ToString();
            FormDays = challenge.Days.ToString();
            FormStartDate = challenge.StartDate;
            // The mode first: a daily amount counts as the Equal amount or the Linear step
            OfferDoubling(challenge.Mode == ChallengeMode.Doubling);
            SelectedMode = Modes.First(m => m.Mode == challenge.Mode);
            SelectedRounding = Roundings.FirstOrDefault(r => r.Cents == challenge.RoundingCents) ?? Roundings[0];
            var dayAmount = challenge.DayAmountCents > 0 ? Money.FromCents(challenge.DayAmountCents).ToString() : string.Empty;
            FormDayAmount = challenge.Mode == ChallengeMode.Equal ? dayAmount : string.Empty;
            FormStep = challenge.Mode == ChallengeMode.Linear ? dayAmount : string.Empty;
            FormFirstDay = challenge.FirstDayCents > 0 ? Money.FromCents(challenge.FirstDayCents).ToString() : string.Empty;
            OpenForm(challenge.CurrencyCode, challenge.ToPocketId != null ? ChallengePlace.Pocket(challenge.ToPocketId.Value)
                : challenge.ToAccountId != null ? ChallengePlace.Account(challenge.ToAccountId.Value) : null);
        }

        private void OpenForm(string currency, ChallengePlace? destination)
        {
            Destination = null;
            _wantedDestination = destination;
            if (FormCurrency == currency)
            {
                BuildDestinationChoices();
                UpdatePreview();
            }
            else
            {
                FormCurrency = currency;
            }
            IsFormOpen = true;
        }

        private void BuildDestinationChoices()
        {
            // Keep what is picked across a currency change when it is still offered
            _wantedDestination = Destination?.Place ?? _wantedDestination;
            var options = new List<ChallengePlaceChoice> { new(null, AppResources.ChallengeNoAccount) };
            options.AddRange(ChallengeSources.Destinations(FormCurrency, _accounts, _pockets).Select(o => new ChallengePlaceChoice(o.Place, o.Label)));
            DestinationOptions = options;
            Destination = options.FirstOrDefault(o => o.Place != null && o.Place == _wantedDestination) ?? options[0];
            _wantedDestination = null;
        }

        private bool HasFirstDayText => IsLinearMode && !string.IsNullOrWhiteSpace(FormFirstDay);

        /// <summary>
        /// The days, the daily amount or step and the first day (cents, 0 = none) the form means: a typed daily amount (the
        /// Equal amount, the Linear step) decides the days, otherwise the days typed. Reads the form only.
        /// </summary>
        private bool TryGetDays(decimal goal, out int days, out long dayAmountCents, out long firstDayCents)
        {
            dayAmountCents = firstDayCents = 0;
            days = 0;
            if (HasFirstDayText)
            {
                var first = AmountParser.Parse(FormFirstDay, FormCurrency);
                if (first <= 0 || !Money.IsInRange(first))
                    return false;
                firstDayCents = Money.ToCents(first);
            }
            if (UsesDayAmount)
            {
                var amount = AmountParser.Parse(DayAmountText, FormCurrency);
                if (amount <= 0 || !Money.IsInRange(amount))
                    return false;
                dayAmountCents = Money.ToCents(amount);
                days = IsLinearMode
                    ? ChallengePlan.DaysForLinear(Money.ToCents(goal), firstDayCents, dayAmountCents)
                    : ChallengePlan.DaysFor(Money.ToCents(goal), dayAmountCents);
                return days <= MaxDays;
            }
            return int.TryParse(FormDays, out days) && days >= 1 && days <= MaxDays;
        }

        /// <summary>A first day with no step that the days already pass the goal with: no step could make the plan.</summary>
        private bool FirstDayTooHigh(decimal goal, int days, long firstDayCents) =>
            !UsesDayAmount && ChallengePlan.FirstDayTooHigh(Money.ToCents(goal), days, firstDayCents);

        /// <summary>A daily amount (or step) so small that the goal would need more days than a challenge can have.</summary>
        private bool TooManyDays(int days, long dayAmountCents) => UsesDayAmount && dayAmountCents > 0 && days > MaxDays;

        private static string TooManyDaysText => string.Format(AppResources.ChallengeTooManyDays, MaxDays.ToString("N0"));

        private void UpdatePreview()
        {
            PreviewDaysText = PreviewTotalText = PlanWarningText = DerivedDays = string.Empty;
            var goal = AmountParser.Parse(FormGoal, FormCurrency);
            if (SelectedMode == null || goal <= 0 || !Money.IsInRange(goal))
                return;
            if (!TryGetDays(goal, out var days, out var dayAmountCents, out var firstDayCents))
            {
                if (TooManyDays(days, dayAmountCents))
                    PlanWarningText = TooManyDaysText;
                return;
            }

            if (UsesDayAmount)
                DerivedDays = days.ToString();
            if (FirstDayTooHigh(goal, days, firstDayCents))
            {
                PlanWarningText = AppResources.ChallengeFirstDayTooHigh;
                return;
            }

            var goalCents = Money.ToCents(goal);
            var plan = ChallengePlan.Build(goalCents, days, SelectedMode.Mode, SelectedRounding.Cents, dayAmountCents, firstDayCents);
            string Format(long cents) => MoneyFormatter.Format(Money.FromCents(cents), FormCurrency);
            PreviewDaysText = SelectedMode.Mode == ChallengeMode.Equal
                ? plan.DayCents[0] == plan.DayCents[^1]
                    ? string.Format(AppResources.ChallengePreviewEqualSame, Format(plan.DayCents[0]))
                    : string.Format(AppResources.ChallengePreviewEqualLast, Format(plan.DayCents[0]), Format(plan.DayCents[^1]))
                : string.Format(AppResources.ChallengePreviewDays, Format(plan.DayCents[0]), Format(plan.DayCents[^1]));
            var difference = plan.TotalCents - goalCents;
            PreviewTotalText = difference == 0
                ? string.Format(AppResources.ChallengePreviewTotal, Format(plan.TotalCents))
                : string.Format(difference > 0 ? AppResources.ChallengePreviewOver : AppResources.ChallengePreviewUnder,
                    Format(plan.TotalCents), Format(Math.Abs(difference)));
            if (SelectedMode.Mode == ChallengeMode.Doubling && plan.UsesLevels)
                PlanWarningText = string.Format(AppResources.ChallengeLevelsWarning, days, plan.MaxUsefulDoublings);
        }

        [RelayCommand]
        private void CloseForm()
        {
            IsFormOpen = false;
            Editing = null;
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
        private async Task SaveChallengeAsync()
        {
            var name = FormName.Trim();
            if (name.Length == 0)
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterChallengeName);
                return;
            }

            var goal = AmountParser.Parse(FormGoal, FormCurrency);
            if (goal <= 0 || !Money.IsInRange(goal))
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterValidAmount);
                return;
            }

            if (!TryGetDays(goal, out var days, out var dayAmountCents, out var firstDayCents))
            {
                if (TooManyDays(days, dayAmountCents))
                {
                    await _errorHandler.HandleAsync(TooManyDaysText);
                    return;
                }
                var badAmount = (UsesDayAmount && dayAmountCents == 0) || (HasFirstDayText && firstDayCents == 0);
                await _errorHandler.HandleAsync(badAmount ? AppResources.PleaseEnterValidAmount : AppResources.PleaseEnterChallengeDays);
                return;
            }
            if (FirstDayTooHigh(goal, days, firstDayCents))
            {
                await _errorHandler.HandleAsync(AppResources.ChallengeFirstDayTooHigh);
                return;
            }

            var adding = !IsEditMode;
            var challenge = new SavingsChallenge
            {
                Id = Editing?.Challenge.Id ?? 0,
                Name = name,
                Goal = goal,
                CurrencyCode = FormCurrency,
                StartDate = FormStartDate.Date,
                Days = days,
                Mode = SelectedMode.Mode,
                RoundingCents = dayAmountCents > 0 ? 0 : SelectedRounding.Cents,
                DayAmountCents = dayAmountCents,
                FirstDayCents = firstDayCents,
                ToAccountId = Destination?.Place?.AccountId,
                ToPocketId = Destination?.Place?.PocketId,
                CreatedDate = Editing?.Challenge.CreatedDate ?? DateTime.Now
            };
            try
            {
                await _challengeService.SaveAsync(challenge);
                CloseForm();
                await LoadChallengesCommand.ExecuteAsync(null);
                if (adding)
                    await _dialogService.ToastAsync(AppResources.ChallengeAdded);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToSaveChallenge);
            }
        }

        /// <summary>Deletes the challenge in the sheet; the sheet stays open behind the question, so cancelling returns to it.</summary>
        [RelayCommand]
        private async Task DeleteChallengeAsync()
        {
            var summary = Editing;
            if (summary == null)
                return;

            var confirm = await _dialogService.ConfirmAsync(
                AppResources.ConfirmDelete,
                string.Format(AppResources.DeleteChallengeConfirmation, summary.Name),
                AppResources.YesDelete,
                AppResources.Cancel);
            if (!confirm)
                return;

            try
            {
                await _challengeService.DeleteAsync(summary.Challenge.Id);
                CloseForm();
                await LoadChallengesCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToDeleteChallenge);
            }
        }
    }
}
