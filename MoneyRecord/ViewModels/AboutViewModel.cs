using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Views;

namespace MoneyRecord.ViewModels
{
    /// <summary>About: version, the app's AGPL licence and source, and the third-party components with their licences.</summary>
    public partial class AboutViewModel : ObservableObject
    {
        public const string SourceCodeUrl = "https://github.com/SebastianOrtizA/MoneyRecord";

        private readonly INavigationService _navigationService;
        private readonly IErrorHandler _errorHandler;
        private readonly IWhatsNewService _whatsNew;

        public AboutViewModel(INavigationService navigationService, IErrorHandler errorHandler, IWhatsNewService whatsNew)
        {
            _navigationService = navigationService;
            _errorHandler = errorHandler;
            _whatsNew = whatsNew;
        }

        public string VersionText => string.Format(AppResources.AppVersion, AppInfo.Current.VersionString);

        public IReadOnlyList<ThirdPartyComponent> Components => ThirdPartyComponent.All;

        [RelayCommand]
        private Task ShowWhatsNewAsync() => _whatsNew.ShowAsync();

        [RelayCommand]
        private Task OpenSourceCodeAsync() => OpenUrlAsync(SourceCodeUrl);

        /// <summary>Shows the component's licence text; one without a file (public domain) opens its website.</summary>
        [RelayCommand]
        private Task OpenComponentAsync(ThirdPartyComponent component) =>
            component.LicenseFile == null
                ? OpenUrlAsync(component.Url)
                : _navigationService.NavigateToAsync(nameof(LicensePage),
                    new Dictionary<string, object> { [LicenseViewModel.ComponentParameter] = component });

        private async Task OpenUrlAsync(string url)
        {
            try
            {
                await Launcher.Default.OpenAsync(new Uri(url));
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToOpenLink);
            }
        }
    }
}
