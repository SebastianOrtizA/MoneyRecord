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

        private List<ChallengeCell> _cells = [];

        public string? ChallengeIdText { get; set; }

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasChallenge), nameof(Title))]
        private ChallengeSummary? summary;

        public bool HasChallenge => Summary != null;

        public string Title => Summary?.Name ?? AppResources.SavingsChallenges;

        /// <summary>The grid: every day, or only those not done while <see cref="ShowRemainingOnly"/>.</summary>
        [ObservableProperty]
        private List<ChallengeCell> cells = [];

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
        }

        public Task InitializeAsync() => LoadCommand.ExecuteAsync(null);

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
                var today = _time.GetLocalNow().Date;
                _cells = loaded == null
                    ? []
                    : loaded.Days.Select(d => new ChallengeCell(d, loaded.Challenge.CurrencyCode, today)).ToList();
                ApplyFilter();
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

        private void ApplyFilter() => Cells = ShowRemainingOnly ? _cells.Where(c => !c.IsDone).ToList() : _cells;

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
