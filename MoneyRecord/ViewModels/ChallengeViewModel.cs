using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>One day of the grid: its date, amount and whether it is done, today or already past.</summary>
    public sealed class ChallengeCell
    {
        public ChallengeCell(ChallengeDayInfo day, string currencyCode, DateTime today)
        {
            Day = day;
            DateText = DateFormatter.Format(day.Date, DateStyle.MonthDay);
            AmountText = MoneyFormatter.Format(Money.FromCents(day.AmountCents), currencyCode);
            IsToday = day.Date.Date == today;
            IsMissed = !day.IsDone && day.Date.Date < today;
            Description = string.Format(AppResources.ChallengeDayDescription, day.DayNumber, AmountText,
                day.IsDone ? AppResources.ChallengeDayDone : AppResources.ChallengeDayPending);
        }

        public ChallengeDayInfo Day { get; }

        public string DateText { get; }

        public string AmountText { get; }

        public string Description { get; }

        public bool IsDone => Day.IsDone;

        public bool IsToday { get; }

        /// <summary>A past day still not done.</summary>
        public bool IsMissed { get; }
    }

    /// <summary>A filter chip: which days the grid shows.</summary>
    public sealed partial class ChallengeViewChip(ChallengeView view, string label, Action<ChallengeView> select) : ObservableObject
    {
        public ChallengeView View { get; } = view;

        public string Label { get; } = label;

        [ObservableProperty]
        private bool isActive;

        /// <summary>False for a month view that holds no day of the challenge: the chip is hidden.</summary>
        [ObservableProperty]
        private bool isAvailable = true;

        [RelayCommand]
        private void Select() => select(View);
    }

    /// <summary>One challenge as a game: a grid with a cell per day; a tap marks it done or not.</summary>
    [QueryProperty(nameof(ChallengeIdText), "ChallengeId")]
    public partial class ChallengeViewModel : ObservableObject
    {
        private readonly ISavingsChallengeService _challengeService;
        private readonly IAccountRepository _accountRepository;
        private readonly IPocketService _pocketService;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;
        private readonly TimeProvider _time;

        private IReadOnlyList<ChallengeDayInfo> _days = [];
        private bool _rangeSet;
        // Where the Next 30 days window starts: pinned for the visit, so marking a day does not slide it away
        private int? _anchorDay;

        public string? ChallengeIdText { get; set; }

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasChallenge), nameof(Title))]
        private ChallengeSummary? summary;

        public bool HasChallenge => Summary != null;

        public string Title => Summary?.Name ?? AppResources.SavingsChallenges;

        /// <summary>The grid: the days of the chosen view (30 days from the first not done by default), or only those not done while <see cref="ShowRemainingOnly"/>.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasNoCells))]
        private List<ChallengeCell> cells = [];

        public bool HasNoCells => HasChallenge && Cells.Count == 0;

        public IReadOnlyList<ChallengeViewChip> ViewChips { get; private set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsRangeView))]
        private ChallengeView selectedView = ChallengeView.Next30;

        public bool IsRangeView => SelectedView == ChallengeView.DateRange;

        partial void OnSelectedViewChanged(ChallengeView value)
        {
            foreach (var chip in ViewChips)
                chip.IsActive = chip.View == value;
            if (value == ChallengeView.Next30)
                _anchorDay = ChallengeDayFilter.FirstPendingDay(_days);
            ApplyFilter();
        }

        /// <summary>"Oct 26 – Nov 24 · days 1–30 of 365": what the grid shows.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasShowing))]
        private string showingText = string.Empty;

        public bool HasShowing => ShowingText.Length > 0;

        [ObservableProperty]
        private DateTime rangeFrom = DateTime.Today;

        partial void OnRangeFromChanged(DateTime value)
        {
            // The range never runs backwards: moving From past To drags To along
            if (value > RangeTo)
                RangeTo = value;
            else
                ApplyFilter();
        }

        [ObservableProperty]
        private DateTime rangeTo = DateTime.Today;

        partial void OnRangeToChanged(DateTime value)
        {
            if (value < RangeFrom)
                RangeFrom = value;
            else
                ApplyFilter();
        }

        [ObservableProperty]
        private bool showRemainingOnly;

        partial void OnShowRemainingOnlyChanged(bool value) => ApplyFilter();

        public ChallengeViewModel(ISavingsChallengeService challengeService, IAccountRepository accountRepository, IPocketService pocketService, IErrorHandler errorHandler, IDialogService dialogService, TimeProvider time)
        {
            _challengeService = challengeService;
            _accountRepository = accountRepository;
            _pocketService = pocketService;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
            _time = time;
            ViewChips =
            [
                new(ChallengeView.Next30, AppResources.ChallengeViewNext30, SelectView) { IsActive = true },
                new(ChallengeView.ThisMonth, AppResources.ChallengeViewThisMonth, SelectView),
                new(ChallengeView.LastMonth, AppResources.ChallengeViewLastMonth, SelectView),
                new(ChallengeView.NextMonth, AppResources.ChallengeViewNextMonth, SelectView),
                new(ChallengeView.GoalEnd, AppResources.ChallengeViewGoalEnd, SelectView),
                new(ChallengeView.DateRange, AppResources.ChallengeViewRange, SelectView)
            ];
        }

        public Task InitializeAsync()
        {
            // A new visit starts the window (and the range) at the first day not done again
            _anchorDay = null;
            _rangeSet = false;
            return LoadCommand.ExecuteAsync(null);
        }

        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadAsync(CancellationToken cancellationToken)
        {
            if (!int.TryParse(ChallengeIdText, out var id))
                return;

            try
            {
                IsLoading = true;
                var loaded = await _challengeService.GetSummaryAsync(id);
                cancellationToken.ThrowIfCancellationRequested();
                Summary = loaded;
                _days = loaded?.Days ?? [];
                _anchorDay ??= ChallengeDayFilter.FirstPendingDay(_days);
                if (loaded != null && !_rangeSet)
                {
                    // The range starts as the same 30 days as the default window; the pickers change it
                    _rangeSet = true;
                    var first = Math.Clamp((_anchorDay ?? 1) - 1, 0, loaded.Days.Count - 1);
                    RangeFrom = loaded.Days[first].Date;
                    RangeTo = loaded.Days[Math.Min(first + ChallengeDayFilter.WindowDays - 1, loaded.Days.Count - 1)].Date;
                }
                UpdateChipAvailability();
                ApplyFilter();
                OnPropertyChanged(nameof(HasNoCells));
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

        /// <summary>Only the cells of the shown days are built: a long challenge stays light.</summary>
        private void ApplyFilter()
        {
            var challenge = Summary?.Challenge;
            if (challenge == null)
            {
                Cells = [];
                return;
            }
            var today = _time.GetLocalNow().Date;
            var shown = ChallengeDayFilter.Select(_days, SelectedView, today, RangeFrom, RangeTo, ShowRemainingOnly, _anchorDay);
            Cells = shown.Select(d => new ChallengeCell(d, challenge.CurrencyCode, today)).ToList();
            ShowingText = shown.Count == 0
                ? string.Empty
                : string.Format(AppResources.ChallengeShowing, DateFormatter.Format(shown[0].Date, DateStyle.MonthDay),
                    DateFormatter.Format(shown[^1].Date, DateStyle.MonthDay), shown[0].DayNumber, shown[^1].DayNumber, _days.Count);
        }

        /// <summary>The month chips hide while the challenge has no day in that month.</summary>
        private void UpdateChipAvailability()
        {
            var today = _time.GetLocalNow().Date;
            foreach (var chip in ViewChips.Where(c => c.View is ChallengeView.ThisMonth or ChallengeView.LastMonth or ChallengeView.NextMonth))
                chip.IsAvailable = ChallengeDayFilter.HasDays(_days, chip.View, today);
            // A month view that has run out (a reload after the month changed) falls back to the default window
            if (ViewChips.Any(c => c.View == SelectedView && !c.IsAvailable))
                SelectedView = ChallengeView.Next30;
        }

        private void SelectView(ChallengeView view) => SelectedView = view;

        [RelayCommand]
        private void ToggleRemainingOnly() => ShowRemainingOnly = !ShowRemainingOnly;

        /// <summary>
        /// Marks the day done (asking where the money comes from when the challenge saves into an account or pocket) or, when
        /// done, unmarks it (asking whether to undo the money it moved).
        /// </summary>
        [RelayCommand]
        private async Task ToggleDayAsync(ChallengeCell? cell)
        {
            var challenge = Summary?.Challenge;
            if (cell == null || challenge == null)
                return;

            try
            {
                var day = cell.Day;
                if (day.IsDone)
                {
                    var undoMoney = false;
                    // The money may be gone already (a transfer deleted on the Transfers page): the question is only a hint
                    if (day.MovedMoney)
                    {
                        var undo = AppResources.ChallengeUnmarkUndo;
                        var keep = AppResources.ChallengeUnmarkKeep;
                        var pick = await _dialogService.ActionSheetAsync(
                            string.Format(AppResources.ChallengeUnmarkTitle, day.DayNumber, cell.AmountText), AppResources.Cancel, [undo, keep]);
                        if (pick != undo && pick != keep)
                            return;
                        undoMoney = pick == undo;
                    }
                    await _challengeService.UnmarkAsync(challenge.Id, day.DayNumber, undoMoney);
                }
                else
                {
                    ChallengePlace? source = null;
                    if (challenge.HasDestination)
                    {
                        var choice = await AskSourceAsync(challenge, cell.AmountText);
                        if (choice.Cancelled)
                            return;
                        source = choice.Source;
                    }
                    await _challengeService.MarkDoneAsync(challenge.Id, day.DayNumber, source);
                }
            }
            catch (PocketProblemException ex)
            {
                var destination = challenge.ToAccountId == null ? null : await _accountRepository.GetByIdAsync(challenge.ToAccountId.Value);
                await _errorHandler.HandleAsync(ex.Problem == PocketProblem.NotEnoughAvailable
                    ? string.Format(AppResources.PocketNotEnoughAvailable, destination?.Name)
                    : AppResources.PocketNotEnoughInPocket);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToUpdateChallenge);
            }
            await LoadCommand.ExecuteAsync(null);
        }

        /// <summary>Which account or pocket the day's money comes from; "just mark the day" has no source.</summary>
        private async Task<(bool Cancelled, ChallengePlace? Source)> AskSourceAsync(SavingsChallenge challenge, string amountText)
        {
            var accounts = await _accountRepository.GetOwnAsync();
            var pockets = await _pocketService.GetPocketsAsync();
            var options = ChallengeSources.Sources(challenge, accounts, pockets);
            var labels = PickList.UniqueLabels(options.Select(o => o.Label));
            var justMark = AppResources.ChallengeJustMark;
            var pick = await _dialogService.ActionSheetAsync(
                string.Format(AppResources.ChallengeSourceTitle, amountText), AppResources.Cancel, [.. labels, justMark]);
            if (pick == justMark)
                return (false, null);
            var index = pick == null ? -1 : labels.IndexOf(pick);
            return index < 0 ? (true, null) : (false, options[index].Place);
        }
    }
}
