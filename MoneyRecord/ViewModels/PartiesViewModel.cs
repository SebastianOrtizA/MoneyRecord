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
    /// <summary>A friend in a party sheet's "who takes part" list (also the expense sheet's "shared between").</summary>
    public partial class PartyMemberChoice(Participant participant, bool isSelected) : ObservableObject
    {
        public Participant Participant { get; } = participant;

        [ObservableProperty]
        private bool isSelected = isSelected;
    }

    /// <summary>Parties page: one card per party (a tap opens it), the friends list, and the new / edit sheet.</summary>
    [QueryProperty(nameof(EditPartyId), "EditPartyId")]
    public partial class PartiesViewModel : ObservableObject
    {
        private readonly IPartyService _parties;
        private readonly IAccountRepository _accountRepository;
        private readonly INavigationService _navigation;
        private readonly IErrorHandler _errorHandler;
        private readonly TimeProvider _clock;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasParties))]
        private List<PartyCard> cards = [];

        public bool HasParties => Cards.Count > 0;

        /// <summary>A party to edit, from the party page's Edit button: its sheet opens once loaded.</summary>
        public string? EditPartyId { get; set; }

        [ObservableProperty]
        private bool isFormOpen;

        /// <summary>The party the sheet edits; null when it creates one.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FormTitle))]
        private PartyEvent? editingParty;

        public string FormTitle => EditingParty != null ? AppResources.EditParty : AppResources.NewParty;

        [ObservableProperty]
        private string formName = string.Empty;

        [ObservableProperty]
        private DateTime formDate = DateTime.Today;

        [ObservableProperty]
        private string formCurrency = string.Empty;

        [ObservableProperty]
        private List<string> currencies = [];

        [ObservableProperty]
        private string formBudget = string.Empty;

        public ObservableCollection<PartyMemberChoice> MemberChoices { get; } = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasNoFriends))]
        private bool hasFriends;

        public bool HasNoFriends => !HasFriends;

        /// <summary>A new friend typed in the sheet; added to the friends list and ticked.</summary>
        [ObservableProperty]
        private string newFriendName = string.Empty;

        public PartiesViewModel(IPartyService parties, IAccountRepository accountRepository, INavigationService navigation,
            IErrorHandler errorHandler, TimeProvider clock)
        {
            _parties = parties;
            _accountRepository = accountRepository;
            _navigation = navigation;
            _errorHandler = errorHandler;
            _clock = clock;
        }

        public async Task InitializeAsync()
        {
            await LoadCommand.ExecuteAsync(null);
            if (int.TryParse(EditPartyId, out var id))
            {
                EditPartyId = null;
                var party = Cards.FirstOrDefault(c => c.PartyId == id)?.Party;
                if (party != null)
                    await OpenFormAsync(party);
            }
        }

        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadAsync(CancellationToken cancellationToken)
        {
            try
            {
                IsLoading = true;
                var summaries = new List<PartyCard>();
                foreach (var party in await _parties.GetPartiesAsync())
                    summaries.Add(new PartyCard(await _parties.GetSummaryAsync(party.Id)));
                cancellationToken.ThrowIfCancellationRequested();
                Cards = summaries;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A newer load replaced this one
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadParties);
            }
            finally
            {
                if (!cancellationToken.IsCancellationRequested)
                    IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task OpenPartyAsync(PartyCard? card)
        {
            if (card != null)
                await _navigation.NavigateToAsync("PartyPage", new Dictionary<string, object> { ["PartyId"] = card.PartyId.ToString() });
        }

        [RelayCommand]
        private Task OpenFriendsAsync() => _navigation.NavigateToAsync("FriendsPage");

        [RelayCommand]
        private Task AddPartyAsync() => OpenFormAsync(null);

        private async Task OpenFormAsync(PartyEvent? party)
        {
            try
            {
                var friends = await _parties.GetParticipantsAsync();
                var members = party == null ? [] : (await _parties.GetMembersAsync(party.Id)).Select(m => m.Id).ToHashSet();
                var accounts = await _accountRepository.GetOwnAsync();
                var baseCode = MoneyFormatter.BaseCurrency;
                Currencies = accounts.Select(a => MoneyFormatter.ResolveCurrencyCode(a.CurrencyCode)).Append(baseCode)
                    .Distinct().OrderBy(code => code != baseCode).ThenBy(code => code, StringComparer.Ordinal).ToList();

                EditingParty = party;
                FormName = party?.Name ?? string.Empty;
                FormDate = party?.Date ?? _clock.GetLocalNow().Date;
                FormCurrency = party == null ? baseCode : MoneyFormatter.ResolveCurrencyCode(party.CurrencyCode);
                FormBudget = party?.Budget is { } budget ? MoneyFormatter.FormatForEntry(budget, FormCurrency) : string.Empty;
                NewFriendName = string.Empty;
                MemberChoices.Clear();
                foreach (var friend in friends)
                    MemberChoices.Add(new PartyMemberChoice(friend, members.Contains(friend.Id)));
                HasFriends = MemberChoices.Count > 0;
                IsFormOpen = true;
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadParties);
            }
        }

        [RelayCommand]
        private async Task AddFriendAsync()
        {
            if (PayeeNames.Normalize(NewFriendName).Length == 0)
                return;

            try
            {
                var friend = await _parties.AddParticipantAsync(NewFriendName);
                NewFriendName = string.Empty;
                var existing = MemberChoices.FirstOrDefault(c => c.Participant.Id == friend.Id);
                if (existing != null)
                    existing.IsSelected = true;
                else
                    MemberChoices.Add(new PartyMemberChoice(friend, true));
                HasFriends = true;
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToSaveFriend);
            }
        }

        [RelayCommand]
        private void CloseForm()
        {
            IsFormOpen = false;
            EditingParty = null;
        }

        /// <summary>Closes the sheet; false when it wasn't open.</summary>
        public bool CloseTopOverlay()
        {
            if (!IsFormOpen)
                return false;

            CloseForm();
            return true;
        }

        [RelayCommand]
        private async Task SavePartyAsync()
        {
            var name = PayeeNames.Normalize(FormName);
            if (name.Length == 0)
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterPartyName);
                return;
            }

            decimal? budget = null;
            if (!string.IsNullOrWhiteSpace(FormBudget))
            {
                budget = AmountParser.Parse(FormBudget, FormCurrency);
                if (budget <= 0 || !Money.IsInRange(budget.Value))
                {
                    await _errorHandler.HandleAsync(AppResources.PleaseEnterValidAmount);
                    return;
                }
            }

            var memberIds = MemberChoices.Where(c => c.IsSelected).Select(c => c.Participant.Id).ToList();
            if (memberIds.Count == 0)
            {
                await _errorHandler.HandleAsync(AppResources.PartyNeedsMembers);
                return;
            }

            // A copy: a refused save must not change the party the list behind the sheet shows
            var party = new PartyEvent
            {
                Id = EditingParty?.Id ?? 0,
                Name = name,
                Date = FormDate.Date,
                CurrencyCode = FormCurrency,
                Budget = budget,
                CreatedDate = EditingParty?.CreatedDate ?? DateTime.Now
            };
            try
            {
                await _parties.SaveAsync(party, memberIds);
                CloseForm();
                await LoadCommand.ExecuteAsync(null);
            }
            catch (PartyProblemException ex) when (ex.Problem is PartyProblem.MemberInUse or PartyProblem.NobodyShares)
            {
                await _errorHandler.HandleAsync(ex.Problem == PartyProblem.MemberInUse
                    ? AppResources.PartyMemberInUse
                    : AppResources.PartyNobodyShares);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToSaveParty);
            }
        }
    }
}
