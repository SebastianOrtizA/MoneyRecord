using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
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

        public ChallengeViewModel(ISavingsChallengeService challengeService, IErrorHandler errorHandler, IDialogService dialogService, TimeProvider time)
        {
            _challengeService = challengeService;
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

        /// <summary>Marks the day done (asking about the transfer when accounts are linked) or, when done, unmarks it.</summary>
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
                    var deleteTransfer = false;
                    // The transfer may be gone already (deleted on the Transfers page): the question is only a hint
                    if (day.TransferId != null)
                    {
                        var delete = AppResources.ChallengeUnmarkDeleteTransfer;
                        var keep = AppResources.ChallengeUnmarkKeepTransfer;
                        var pick = await _dialogService.ActionSheetAsync(
                            string.Format(AppResources.ChallengeUnmarkTitle, day.DayNumber, cell.AmountText), AppResources.Cancel, [delete, keep]);
                        if (pick != delete && pick != keep)
                            return;
                        deleteTransfer = pick == delete;
                    }
                    await _challengeService.UnmarkAsync(challenge.Id, day.DayNumber, deleteTransfer);
                }
                else
                {
                    var recordTransfer = false;
                    if (challenge.HasAccounts)
                    {
                        var record = AppResources.ChallengeRecordTransferYes;
                        var justMark = AppResources.ChallengeJustMark;
                        var pick = await _dialogService.ActionSheetAsync(
                            string.Format(AppResources.ChallengeRecordTransfer, cell.AmountText), AppResources.Cancel, [record, justMark]);
                        if (pick != record && pick != justMark)
                            return;
                        recordTransfer = pick == record;
                    }
                    await _challengeService.MarkDoneAsync(challenge.Id, day.DayNumber, recordTransfer);
                }
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToUpdateChallenge);
            }
            await LoadCommand.ExecuteAsync(null);
        }
    }
}
