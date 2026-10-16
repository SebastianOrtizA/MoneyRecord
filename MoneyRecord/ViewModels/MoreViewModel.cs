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

        [ObservableProperty]
        private IReadOnlyList<MoreItem> items = BuildItems();

        public MoreViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService;
            // The More page lives as long as the app, so the titles follow a language change in Settings
            LocalizationService.Instance.LanguageChanged += (_, _) => Items = BuildItems();
        }

        private static IReadOnlyList<MoreItem> BuildItems() =>
        [
            new("\U000F0070", AppResources.Accounts, nameof(ManageAccountsPage)),
            new("\U000F0831", AppResources.Categories, nameof(ManageCategoriesPage)),
            new("\U000F0456", AppResources.RecurringTransactions, nameof(RecurringTransactionsPage)),
            new("\U000F1007", AppResources.SavingsGoals, nameof(GoalsPage)),
            new("\U000F04E1", AppResources.Transfers, nameof(TransfersPage)),
            new("\U000F0493", AppResources.Settings, nameof(SettingsPage)),
        ];

        [RelayCommand]
        private Task OpenAsync(MoreItem item) => _navigationService.NavigateToAsync(item.Route);
    }
}
