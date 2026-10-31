using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>One day of the grid: its date, amount and whether it is done, today or already past; or an empty calendar slot.</summary>
    public sealed class ChallengeCell
    {
        /// <param name="showMonth">The date reads "Oct 27"; otherwise just the day number (the month is named nearby).</param>
        public ChallengeCell(ChallengeDayInfo day, string currencyCode, DateTime today, bool showMonth)
        {
            Day = day;
            DateText = showMonth ? DateFormatter.Format(day.Date, DateStyle.MonthDay) : day.Date.Day.ToString(CultureInfo.CurrentCulture);
            AmountText = MoneyFormatter.Format(Money.FromCents(day.AmountCents), currencyCode);
            IsToday = day.Date.Date == today;
            IsMissed = !day.IsDone && day.Date.Date < today;
            Description = string.Format(AppResources.ChallengeDayDescription, day.DayNumber, AmountText,
                day.IsDone ? AppResources.ChallengeDayDone : AppResources.ChallengeDayPending,
                DateFormatter.Format(day.Date, DateStyle.Long));
        }

        /// <summary>An empty slot of the month calendar: it takes room but shows nothing.</summary>
        private ChallengeCell()
        {
            DateText = AmountText = Description = string.Empty;
        }

        public static ChallengeCell Blank() => new();

        public ChallengeDayInfo? Day { get; }

        public bool IsBlank => Day == null;

        public string DateText { get; }

        public string AmountText { get; }

        public string Description { get; }

        public bool IsDone => Day?.IsDone ?? false;

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

        [RelayCommand]
        private void Select() => select(View);
    }

    /// <summary>One challenge as a game: a grid with a cell per day; a tap marks it done or not.</summary>
    [QueryProperty(nameof(ChallengeIdText), "ChallengeId")]
    public partial class ChallengeViewModel : ObservableObject, IPeriodStepperSource
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
        [NotifyPropertyChangedFor(nameof(IsRangeView), nameof(IsMonthView))]
        private ChallengeView selectedView = ChallengeView.Next30;

        public bool IsRangeView => SelectedView == ChallengeView.DateRange;

        public bool IsMonthView => SelectedView == ChallengeView.Month;

        partial void OnSelectedViewChanged(ChallengeView value)
        {
            foreach (var chip in ViewChips)
                chip.IsActive = chip.View == value;
            if (value == ChallengeView.Next30)
                _anchorDay = ChallengeDayFilter.FirstPendingDay(_days);
            if (value == ChallengeView.Month)
                ShownMonth = ChallengeDayFilter.ClampMonth(_days, _time.GetLocalNow().Date);
            UpdateMonthNavigation();
            ApplyFilter();
        }

        /// <summary>The month of the calendar view (its 1st), always one the challenge has days in.</summary>
        [ObservableProperty]
        private DateTime shownMonth;

        // The cells are rebuilt by whoever moves the month, once: the commands below, the view switch and the load
        partial void OnShownMonthChanged(DateTime value) => UpdateMonthNavigation();

        /// <summary>"October 2026": the month the calendar shows.</summary>
        [ObservableProperty]
        private string label = string.Empty;

        [ObservableProperty]
        private bool canStepBack;

        [ObservableProperty]
        private bool canStepForward;

        /// <summary>The weekday names over the calendar columns, starting at the culture's first day of the week.</summary>
        public IReadOnlyList<string> WeekdayHeaders { get; } = ChallengeDayFilter.WeekdayHeaders(CultureInfo.CurrentCulture);

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
            shownMonth = ChallengeDayFilter.ClampMonth([], time.GetLocalNow().Date);
            ViewChips =
            [
                new(ChallengeView.FromStart, AppResources.ChallengeViewFromStart, SelectView),
                new(ChallengeView.Month, AppResources.ChallengeViewThisMonth, SelectView),
                new(ChallengeView.Next30, AppResources.ChallengeViewNext30, SelectView) { IsActive = true },
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
                // A reload keeps the month shown, unless the challenge changed under it
                ShownMonth = ChallengeDayFilter.ClampMonth(_days, IsMonthView ? ShownMonth : _time.GetLocalNow().Date);
                UpdateMonthNavigation();
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
            var shown = ChallengeDayFilter.Select(_days, SelectedView, ShownMonth, RangeFrom, RangeTo, ShowRemainingOnly, _anchorDay);
            if (IsMonthView)
            {
                // The calendar keeps empty slots so every day sits under its weekday; the month is named above it
                var slots = ChallengeDayFilter.MonthSlots(_days, ShownMonth, ShowRemainingOnly, CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek);
                Cells = slots.Select(d => d == null ? ChallengeCell.Blank() : new ChallengeCell(d, challenge.CurrencyCode, today, false)).ToList();
            }
            else
            {
                // The month is named on the first cell and wherever a month starts
                Cells = shown.Select((d, i) => new ChallengeCell(d, challenge.CurrencyCode, today, i == 0 || d.Date.Day == 1)).ToList();
            }
            ShowingText = shown.Count == 0
                ? string.Empty
                : string.Format(AppResources.ChallengeShowing, DateFormatter.Format(shown[0].Date, DateStyle.MonthDay),
                    DateFormatter.Format(shown[^1].Date, DateStyle.MonthDay), shown[0].DayNumber, shown[^1].DayNumber, _days.Count);
        }

        private void UpdateMonthNavigation()
        {
            Label = DateFormatter.FormatMonthYear(ShownMonth);
            var span = ChallengeDayFilter.MonthSpan(_days);
            CanStepBack = span != null && ShownMonth > span.Value.First;
            CanStepForward = span != null && ShownMonth < span.Value.Last;
        }

        private void SelectView(ChallengeView view) => SelectedView = view;

        [RelayCommand]
        private void Previous()
        {
            if (!CanStepBack)
                return;
            ShownMonth = ShownMonth.AddMonths(-1);
            ApplyFilter();
        }

        [RelayCommand]
        private void Next()
        {
            if (!CanStepForward)
                return;
            ShownMonth = ShownMonth.AddMonths(1);
            ApplyFilter();
        }

        /// <summary>Back to the current month (or the nearest one the challenge has).</summary>
        [RelayCommand]
        private void CurrentMonth()
        {
            ShownMonth = ChallengeDayFilter.ClampMonth(_days, _time.GetLocalNow().Date);
            ApplyFilter();
        }

        // The stepper's label goes back to the current month; there is no list to open
        IRelayCommand IPeriodStepperSource.PickCommand => CurrentMonthCommand;

        bool IPeriodStepperSource.HasPicker => false;

        string IPeriodStepperSource.PickHint => AppResources.ChallengeMonthCurrent;

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
            if (cell?.Day is not { } day || challenge == null)
                return;

            try
            {
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
