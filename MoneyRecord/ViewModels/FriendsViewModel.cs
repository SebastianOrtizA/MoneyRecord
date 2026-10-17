using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>The friends list shared by every party: add by name, a tap offers rename / delete.</summary>
    public partial class FriendsViewModel : ObservableObject
    {
        private readonly IPartyService _parties;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEmpty))]
        private List<Participant> friends = [];

        public bool IsEmpty => Friends.Count == 0;

        [ObservableProperty]
        private string newName = string.Empty;

        public FriendsViewModel(IPartyService parties, IErrorHandler errorHandler, IDialogService dialogService)
        {
            _parties = parties;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
        }

        public Task InitializeAsync() => LoadCommand.ExecuteAsync(null);

        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadAsync(CancellationToken cancellationToken)
        {
            try
            {
                IsLoading = true;
                var loaded = await _parties.GetParticipantsAsync();
                cancellationToken.ThrowIfCancellationRequested();
                Friends = loaded;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A newer load replaced this one
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadFriends);
            }
            finally
            {
                if (!cancellationToken.IsCancellationRequested)
                    IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task AddAsync()
        {
            if (PayeeNames.Normalize(NewName).Length == 0)
                return;

            try
            {
                await _parties.AddParticipantAsync(NewName);
                NewName = string.Empty;
                await LoadCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToSaveFriend);
            }
        }

        [RelayCommand]
        private async Task OpenAsync(Participant? friend)
        {
            if (friend == null)
                return;

            var choice = await _dialogService.ActionSheetAsync(friend.Name, AppResources.Cancel,
                [AppResources.RenameFriend, AppResources.DeleteButton]);
            if (choice == AppResources.RenameFriend)
                await RenameAsync(friend);
            else if (choice == AppResources.DeleteButton)
                await DeleteAsync(friend);
        }

        private async Task RenameAsync(Participant friend)
        {
            var name = await _dialogService.PromptAsync(AppResources.RenameFriendTitle, string.Empty, AppResources.Save,
                AppResources.Cancel, friend.Name);
            if (name == null || PayeeNames.Normalize(name).Length == 0)
                return;

            try
            {
                await _parties.RenameParticipantAsync(friend.Id, name);
                await LoadCommand.ExecuteAsync(null);
            }
            catch (PartyProblemException ex) when (ex.Problem == PartyProblem.NameTaken)
            {
                await _errorHandler.HandleAsync(AppResources.FriendNameTaken);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToSaveFriend);
            }
        }

        private async Task DeleteAsync(Participant friend)
        {
            if (!await _dialogService.ConfirmAsync(AppResources.ConfirmDelete,
                    string.Format(AppResources.DeleteFriendQuestion, friend.Name), AppResources.YesDelete, AppResources.Cancel))
                return;

            try
            {
                await _parties.DeleteParticipantAsync(friend.Id);
                await LoadCommand.ExecuteAsync(null);
            }
            catch (PartyProblemException ex) when (ex.Problem == PartyProblem.FriendInParty)
            {
                await _errorHandler.HandleAsync(AppResources.FriendInParty);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToSaveFriend);
            }
        }
    }
}
