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

    /// <summary>An account to take from / save in; a null <see cref="Id"/> is "none".</summary>
    public sealed record ChallengeAccountOption(int? Id, string Label)
    {
        public override string ToString() => Label;
    }

    /// <summary>Challenges page: one card per challenge (a tap opens the edit sheet) and the add/edit sheet with a live preview.</summary>
    public partial class ChallengesViewModel : ObservableObject
    {
        private const int MaxDays = 3650;

        private readonly ISavingsChallengeService _challengeService;
        private readonly IAccountRepository _accountRepository;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;

        private List<Account> _accounts = [];

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
        [NotifyPropertyChangedFor(nameof(IsEditMode), nameof(FormTitle), nameof(SaveText), nameof(IsPlanEditable), nameof(IsPlanLocked))]
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

        partial void OnFormDaysChanged(string value) => UpdatePreview();

        [ObservableProperty]
        private DateTime formStartDate = DateTime.Today;

        [ObservableProperty]
        private string formCurrency = string.Empty;

        partial void OnFormCurrencyChanged(string value)
        {
            BuildAccountChoices();
            UpdatePreview();
        }

        /// <summary>The currencies of the accounts, base currency first.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowCurrencyPicker))]
        private List<string> currencies = [];

        public bool ShowCurrencyPicker => Currencies.Count > 1;

        public IReadOnlyList<ChallengeModeOption> Modes { get; private set; } = BuildModes();

        public IReadOnlyList<ChallengeRoundingOption> Roundings { get; private set; } = BuildRoundings();

        [ObservableProperty]
        private ChallengeModeOption selectedMode;

        partial void OnSelectedModeChanged(ChallengeModeOption value) => UpdatePreview();

        [ObservableProperty]
        private ChallengeRoundingOption selectedRounding;

        partial void OnSelectedRoundingChanged(ChallengeRoundingOption value) => UpdatePreview();

        /// <summary>The accounts in the challenge's currency, after "none".</summary>
        [ObservableProperty]
        private List<ChallengeAccountOption> accountOptions = [];

        [ObservableProperty]
        private ChallengeAccountOption? fromAccount;

        [ObservableProperty]
        private ChallengeAccountOption? toAccount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasPreview))]
        private string previewDaysText = string.Empty;

        [ObservableProperty]
        private string previewTotalText = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasLevelsWarning))]
        private string levelsWarningText = string.Empty;

        public bool HasPreview => PreviewDaysText.Length > 0;

        public bool HasLevelsWarning => LevelsWarningText.Length > 0;

        public ChallengesViewModel(ISavingsChallengeService challengeService, IAccountRepository accountRepository, IErrorHandler errorHandler,
            IDialogService dialogService)
        {
            _challengeService = challengeService;
            _accountRepository = accountRepository;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
            selectedMode = Modes[0];
            selectedRounding = Roundings[0];
            // The page lives as long as the app: the labels follow a language change
            LocalizationService.Instance.LanguageChanged += (_, _) => RebuildLabels();
        }

        private static List<ChallengeModeOption> BuildModes() =>
        [
            new(ChallengeMode.Doubling, AppResources.ChallengeModeDoubling),
            new(ChallengeMode.Linear, AppResources.ChallengeModeLinear)
        ];

        private static List<ChallengeRoundingOption> BuildRoundings() =>
        [
            new(0, AppResources.ChallengeRoundingNone),
            .. new[] { 1, 10, 100, 1000 }.Select(units =>
                new ChallengeRoundingOption(units * 100L, string.Format(AppResources.ChallengeRoundTo, units.ToString("N0"))))
        ];

        private void RebuildLabels()
        {
            var mode = SelectedMode.Mode;
            var rounding = SelectedRounding.Cents;
            Modes = BuildModes();
            Roundings = BuildRoundings();
            OnPropertyChanged(nameof(Modes));
            OnPropertyChanged(nameof(Roundings));
            SelectedMode = Modes.First(m => m.Mode == mode);
            SelectedRounding = Roundings.First(r => r.Cents == rounding);
        }

        public Task InitializeAsync() => LoadChallengesCommand.ExecuteAsync(null);

        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadChallengesAsync(CancellationToken cancellationToken)
        {
            try
            {
                IsLoading = true;

                var challengesTask = _challengeService.GetSummariesAsync();
                var accountsTask = _accountRepository.GetOwnAsync();
                await Task.WhenAll(challengesTask, accountsTask);
                cancellationToken.ThrowIfCancellationRequested();

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

        [RelayCommand]
        private void AddChallenge()
        {
            Editing = null;
            FormName = string.Empty;
            FormGoal = string.Empty;
            FormDays = "30";
            FormStartDate = DateTime.Today;
            SelectedMode = Modes[0];
            SelectedRounding = Roundings[0];
            OpenForm(Currencies.FirstOrDefault() ?? MoneyFormatter.BaseCurrency, null, null);
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
            SelectedMode = Modes.First(m => m.Mode == challenge.Mode);
            SelectedRounding = Roundings.FirstOrDefault(r => r.Cents == challenge.RoundingCents) ?? Roundings[0];
            OpenForm(challenge.CurrencyCode, challenge.FromAccountId, challenge.ToAccountId);
        }

        private int? _wantedFrom;
        private int? _wantedTo;

        private void OpenForm(string currency, int? from, int? to)
        {
            FromAccount = ToAccount = null;
            _wantedFrom = from;
            _wantedTo = to;
            if (FormCurrency == currency)
            {
                BuildAccountChoices();
                UpdatePreview();
            }
            else
            {
                FormCurrency = currency;
            }
            IsFormOpen = true;
        }

        private void BuildAccountChoices()
        {
            // Keep what is picked across a currency change when it is still offered
            _wantedFrom = FromAccount?.Id ?? _wantedFrom;
            _wantedTo = ToAccount?.Id ?? _wantedTo;
            var options = new List<ChallengeAccountOption> { new(null, AppResources.ChallengeNoAccount) };
            options.AddRange(_accounts
                .Where(a => MoneyFormatter.SameCurrency(a.CurrencyCode, FormCurrency))
                .Select(a => new ChallengeAccountOption(a.Id, a.Name)));
            AccountOptions = options;
            FromAccount = options.FirstOrDefault(o => o.Id != null && o.Id == _wantedFrom) ?? options[0];
            ToAccount = options.FirstOrDefault(o => o.Id != null && o.Id == _wantedTo) ?? options[0];
            _wantedFrom = _wantedTo = null;
        }

        private void UpdatePreview()
        {
            PreviewDaysText = PreviewTotalText = LevelsWarningText = string.Empty;
            var goal = AmountParser.Parse(FormGoal, FormCurrency);
            if (goal <= 0 || !Money.IsInRange(goal) || !int.TryParse(FormDays, out var days) || days < 1 || days > MaxDays)
                return;

            var goalCents = Money.ToCents(goal);
            var plan = ChallengePlan.Build(goalCents, days, SelectedMode.Mode, SelectedRounding.Cents);
            string Format(long cents) => MoneyFormatter.Format(Money.FromCents(cents), FormCurrency);
            PreviewDaysText = string.Format(AppResources.ChallengePreviewDays, Format(plan.DayCents[0]), Format(plan.DayCents[^1]));
            var difference = plan.TotalCents - goalCents;
            PreviewTotalText = difference == 0
                ? string.Format(AppResources.ChallengePreviewTotal, Format(plan.TotalCents))
                : string.Format(difference > 0 ? AppResources.ChallengePreviewOver : AppResources.ChallengePreviewUnder,
                    Format(plan.TotalCents), Format(Math.Abs(difference)));
            if (SelectedMode.Mode == ChallengeMode.Doubling && plan.UsesLevels)
                LevelsWarningText = string.Format(AppResources.ChallengeLevelsWarning, days, plan.MaxUsefulDoublings);
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

            if (!int.TryParse(FormDays, out var days) || days < 1 || days > MaxDays)
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterChallengeDays);
                return;
            }

            var from = FromAccount?.Id;
            var to = ToAccount?.Id;
            if ((from == null) != (to == null) || (from != null && from == to))
            {
                await _errorHandler.HandleAsync(AppResources.PleaseSelectChallengeAccounts);
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
                RoundingCents = SelectedRounding.Cents,
                FromAccountId = from,
                ToAccountId = to,
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
