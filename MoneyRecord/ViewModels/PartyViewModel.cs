using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>Who paid in the expense sheet: a member, or the common pot (<see cref="Participant"/> null).</summary>
    public sealed record PartyPayerChoice(Participant? Participant, string Name);

    /// <summary>One party: spent against its budget, who is owed or owes, its expenses and contributions, and their sheets.</summary>
    [QueryProperty(nameof(PartyIdText), "PartyId")]
    public partial class PartyViewModel : ObservableObject
    {
        private readonly IPartyService _parties;
        private readonly INavigationService _navigation;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;
        private readonly TimeProvider _clock;

        private List<Participant> _members = [];

        public string? PartyIdText { get; set; }

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasParty), nameof(Title), nameof(CurrencyCode))]
        private PartyCard? card;

        public bool HasParty => Card != null;

        public string Title => Card?.Name ?? AppResources.PartiesTitle;

        public string CurrencyCode => Card?.Party.CurrencyCode ?? string.Empty;

        [ObservableProperty]
        private List<PartyBalanceRow> balances = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasNoExpenses))]
        private List<PartyExpenseRow> expenses = [];

        public bool HasNoExpenses => Expenses.Count == 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasNoContributions))]
        private List<PartyContributionRow> contributions = [];

        public bool HasNoContributions => Contributions.Count == 0;

        public PartyViewModel(IPartyService parties, INavigationService navigation, IErrorHandler errorHandler,
            IDialogService dialogService, TimeProvider clock)
        {
            _parties = parties;
            _navigation = navigation;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
            _clock = clock;
        }

        public Task InitializeAsync() => LoadCommand.ExecuteAsync(null);

        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadAsync(CancellationToken cancellationToken)
        {
            if (!int.TryParse(PartyIdText, out var partyId))
                return;

            try
            {
                IsLoading = true;
                var summary = await _parties.GetSummaryAsync(partyId);
                var members = await _parties.GetMembersAsync(partyId);
                var expenseList = await _parties.GetExpensesAsync(partyId);
                var exclusions = await _parties.GetExclusionsAsync(partyId);
                var contributionList = await _parties.GetContributionsAsync(partyId);
                cancellationToken.ThrowIfCancellationRequested();

                var names = members.ToDictionary(m => m.Id, m => m.Name);
                var code = summary.Party.CurrencyCode;
                _members = members;
                Card = new PartyCard(summary);
                var potUsed = contributionList.Count > 0 || expenseList.Any(e => e.PaidByParticipantId == null);
                Balances = summary.Balances
                    .Where(b => b.ParticipantId != PartyLedger.PotId || potUsed)
                    .Select(b => new PartyBalanceRow(b, code)).ToList();
                Expenses = expenseList.Select(e => new PartyExpenseRow(e,
                    e.Description.Length > 0 ? e.Description : PayerName(e.PaidByParticipantId, names),
                    string.Join(" · ", DateFormatter.Format(e.Date, DateStyle.Medium),
                        string.Format(AppResources.PartyPaidByLine, PayerName(e.PaidByParticipantId, names)),
                        string.Format(AppResources.PartySharedLine,
                            members.Count - (exclusions.TryGetValue(e.Id, out var left) ? left.Count : 0), members.Count)),
                    MoneyFormatter.Format(e.Amount, code))).ToList();
                Contributions = contributionList.Select(c => new PartyContributionRow(c,
                    names.GetValueOrDefault(c.ParticipantId, string.Empty),
                    DateFormatter.Format(c.Date, DateStyle.Medium),
                    MoneyFormatter.Format(c.Amount, code))).ToList();
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

        private static string PayerName(int? participantId, Dictionary<int, string> names) =>
            participantId is { } id ? names.GetValueOrDefault(id, string.Empty) : AppResources.PartyPaidByPot;

        // ----- Expense sheet -----

        [ObservableProperty]
        private bool isExpenseSheetOpen;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ExpenseSheetTitle))]
        private PartyExpense? editingExpense;

        public string ExpenseSheetTitle => EditingExpense != null ? AppResources.EditPartyExpense : AppResources.AddPartyExpense;

        [ObservableProperty]
        private string expenseDescription = string.Empty;

        [ObservableProperty]
        private string expenseAmount = string.Empty;

        [ObservableProperty]
        private DateTime expenseDate = DateTime.Today;

        [ObservableProperty]
        private List<PartyPayerChoice> payerChoices = [];

        [ObservableProperty]
        private PartyPayerChoice? selectedPayer;

        public ObservableCollection<PartyMemberChoice> SharerChoices { get; } = [];

        [RelayCommand]
        private Task AddExpenseAsync() => OpenExpenseSheetAsync(null);

        [RelayCommand]
        private async Task OpenExpenseAsync(PartyExpenseRow? row)
        {
            if (row == null)
                return;

            var choice = await _dialogService.ActionSheetAsync(row.Title, AppResources.Cancel,
                [AppResources.Edit, AppResources.DeleteButton]);
            if (choice == AppResources.Edit)
                await OpenExpenseSheetAsync(row.Expense);
            else if (choice == AppResources.DeleteButton)
                await DeleteExpenseAsync(row.Expense);
        }

        private async Task OpenExpenseSheetAsync(PartyExpense? expense)
        {
            if (Card == null)
                return;

            try
            {
                var excluded = expense == null
                    ? []
                    : (await _parties.GetExclusionsAsync(Card.PartyId)).GetValueOrDefault(expense.Id) ?? [];
                EditingExpense = expense;
                ExpenseDescription = expense?.Description ?? string.Empty;
                ExpenseAmount = expense == null ? string.Empty : MoneyFormatter.FormatForEntry(expense.Amount, CurrencyCode);
                ExpenseDate = expense?.Date ?? _clock.GetLocalNow().Date;
                PayerChoices = [.. _members.Select(m => new PartyPayerChoice(m, m.Name)), new PartyPayerChoice(null, AppResources.PartyPaidByPot)];
                // A new expense starts with the first member as payer; an edited one with its own (null = the pot)
                SelectedPayer = expense == null
                    ? PayerChoices[0]
                    : PayerChoices.FirstOrDefault(p => p.Participant?.Id == expense.PaidByParticipantId) ?? PayerChoices[0];
                SharerChoices.Clear();
                foreach (var member in _members)
                    SharerChoices.Add(new PartyMemberChoice(member, !excluded.Contains(member.Id)));
                IsExpenseSheetOpen = true;
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadParties);
            }
        }

        [RelayCommand]
        private void CloseExpenseSheet()
        {
            IsExpenseSheetOpen = false;
            EditingExpense = null;
        }

        [RelayCommand]
        private async Task SaveExpenseAsync()
        {
            if (Card == null)
                return;

            var amount = AmountParser.Parse(ExpenseAmount, CurrencyCode);
            if (amount <= 0 || !Money.IsInRange(amount))
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterValidAmount);
                return;
            }

            if (SharerChoices.All(c => !c.IsSelected))
            {
                await _errorHandler.HandleAsync(AppResources.PartyNeedsSharer);
                return;
            }

            var expense = EditingExpense ?? new PartyExpense { PartyId = Card.PartyId };
            expense.Description = ExpenseDescription;
            expense.Amount = amount;
            expense.Date = ExpenseDate.Date;
            expense.PaidByParticipantId = SelectedPayer?.Participant?.Id;
            try
            {
                await _parties.SaveExpenseAsync(expense, SharerChoices.Where(c => !c.IsSelected).Select(c => c.Participant.Id).ToList());
                CloseExpenseSheet();
                await LoadCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToSavePartyExpense);
            }
        }

        private async Task DeleteExpenseAsync(PartyExpense expense)
        {
            if (!await _dialogService.ConfirmAsync(AppResources.ConfirmDelete, AppResources.DeletePartyExpenseQuestion,
                    AppResources.YesDelete, AppResources.Cancel))
                return;

            try
            {
                await _parties.DeleteExpenseAsync(expense.Id);
                await LoadCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToSavePartyExpense);
            }
        }

        // ----- Contribution sheet -----

        [ObservableProperty]
        private bool isContributionSheetOpen;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ContributionSheetTitle))]
        private PartyContribution? editingContribution;

        public string ContributionSheetTitle =>
            EditingContribution != null ? AppResources.EditPartyContribution : AppResources.AddPartyContribution;

        [ObservableProperty]
        private string contributionAmount = string.Empty;

        [ObservableProperty]
        private DateTime contributionDate = DateTime.Today;

        [ObservableProperty]
        private List<Participant> memberChoices = [];

        [ObservableProperty]
        private Participant? selectedContributor;

        [RelayCommand]
        private void AddContribution() => OpenContributionSheet(null);

        [RelayCommand]
        private async Task OpenContributionAsync(PartyContributionRow? row)
        {
            if (row == null)
                return;

            var choice = await _dialogService.ActionSheetAsync(row.Title, AppResources.Cancel,
                [AppResources.Edit, AppResources.DeleteButton]);
            if (choice == AppResources.Edit)
                OpenContributionSheet(row.Contribution);
            else if (choice == AppResources.DeleteButton)
                await DeleteContributionAsync(row.Contribution);
        }

        private void OpenContributionSheet(PartyContribution? contribution)
        {
            if (Card == null)
                return;

            EditingContribution = contribution;
            ContributionAmount = contribution == null ? string.Empty : MoneyFormatter.FormatForEntry(contribution.Amount, CurrencyCode);
            ContributionDate = contribution?.Date ?? _clock.GetLocalNow().Date;
            MemberChoices = _members;
            SelectedContributor = contribution == null
                ? _members.FirstOrDefault()
                : _members.FirstOrDefault(m => m.Id == contribution.ParticipantId);
            IsContributionSheetOpen = true;
        }

        [RelayCommand]
        private void CloseContributionSheet()
        {
            IsContributionSheetOpen = false;
            EditingContribution = null;
        }

        [RelayCommand]
        private async Task SaveContributionAsync()
        {
            if (Card == null)
                return;

            if (SelectedContributor == null)
            {
                await _errorHandler.HandleAsync(AppResources.PartyNeedsMembers);
                return;
            }

            var amount = AmountParser.Parse(ContributionAmount, CurrencyCode);
            if (amount <= 0 || !Money.IsInRange(amount))
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterValidAmount);
                return;
            }

            var contribution = EditingContribution ?? new PartyContribution { PartyId = Card.PartyId };
            contribution.ParticipantId = SelectedContributor.Id;
            contribution.Amount = amount;
            contribution.Date = ContributionDate.Date;
            try
            {
                await _parties.SaveContributionAsync(contribution);
                CloseContributionSheet();
                await LoadCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToSavePartyContribution);
            }
        }

        private async Task DeleteContributionAsync(PartyContribution contribution)
        {
            if (!await _dialogService.ConfirmAsync(AppResources.ConfirmDelete, AppResources.DeletePartyContributionQuestion,
                    AppResources.YesDelete, AppResources.Cancel))
                return;

            try
            {
                await _parties.DeleteContributionAsync(contribution.Id);
                await LoadCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToSavePartyContribution);
            }
        }

        /// <summary>Closes the open sheet; false when none was open.</summary>
        public bool CloseTopOverlay()
        {
            if (IsExpenseSheetOpen)
                CloseExpenseSheet();
            else if (IsContributionSheetOpen)
                CloseContributionSheet();
            else
                return false;

            return true;
        }

        // ----- The party itself -----

        [RelayCommand]
        private async Task EditAsync()
        {
            if (Card != null)
                await _navigation.NavigateToAsync("PartiesPage", new Dictionary<string, object> { ["EditPartyId"] = Card.PartyId.ToString() });
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (Card == null)
                return;

            if (!await _dialogService.ConfirmAsync(AppResources.ConfirmDelete, AppResources.DeletePartyQuestion,
                    AppResources.YesDelete, AppResources.Cancel))
                return;

            try
            {
                await _parties.DeleteAsync(Card.PartyId);
                await _navigation.GoBackAsync();
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToDeleteParty);
            }
        }
    }
}
