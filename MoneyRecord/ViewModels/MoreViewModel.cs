using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Views;

namespace MoneyRecord.ViewModels
{
    /// <summary>A More row: icon glyph, localized title and the registered route it pushes.</summary>
    public record MoreItem(string Icon, string Title, string Route);

    /// <summary>More tab: opens the screens that don't have a tab of their own (pushed, so back returns here).</summary>
    public partial class MoreViewModel : ObservableObject
    {
        private readonly INavigationService _navigationService;
        private readonly ITipJar _tipJar;

        [ObservableProperty]
        private IReadOnlyList<MoreItem> items = [];

        public MoreViewModel(INavigationService navigationService, ITipJar tipJar)
        {
            _navigationService = navigationService;
            _tipJar = tipJar;
            Items = BuildItems();
            // The More page lives as long as the app, so the titles follow a language change in Settings
            LocalizationService.Instance.LanguageChanged += (_, _) => Items = BuildItems();
        }

        private IReadOnlyList<MoreItem> BuildItems() =>
        [
            new("\U000F0070", AppResources.Accounts, nameof(ManageAccountsPage)),
            new("\U000F0831", AppResources.Categories, nameof(ManageCategoriesPage)),
            new("\U000F04DC", AppResources.Payees, nameof(PayeesPage)),
            new("\U000F0456", AppResources.RecurringTransactions, nameof(RecurringTransactionsPage)),
            new("\U000F1007", AppResources.SavingsGoals, nameof(GoalsPage)),
            new("\U000F0538", AppResources.SavingsChallenges, nameof(ChallengesPage)),
            new("\U000F1218", AppResources.Debts, nameof(DebtsPage)),
            new("\U000F001D", AppResources.TripsTitle, nameof(TripsPage)),
            new("\U000F0849", AppResources.PartiesTitle, nameof(PartiesPage)),
            new("\U000F04E1", AppResources.Transfers, nameof(TransfersPage)),
            // Tips are sold in the Google Play build only
            .. (_tipJar.IsAvailable ? new MoreItem[] { new("\U000F02D1", AppResources.SupportTitle, nameof(SupportPage)) } : []),
            new("\U000F0493", AppResources.Settings, nameof(SettingsPage)),
        ];

        [RelayCommand]
        private Task OpenAsync(MoreItem item) => _navigationService.NavigateToAsync(item.Route);
    }
}
