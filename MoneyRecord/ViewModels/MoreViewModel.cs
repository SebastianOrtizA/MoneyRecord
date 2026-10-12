using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>More tab: opens the screens that don't have a tab of their own (pushed, so back returns here).</summary>
    public partial class MoreViewModel
    {
        private readonly INavigationService _navigationService;

        public MoreViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService;
        }

        /// <summary>Route: a page name registered in AppShell.</summary>
        [RelayCommand]
        private Task OpenAsync(string route) => _navigationService.NavigateToAsync(route);
    }
}
