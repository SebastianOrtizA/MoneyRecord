using CommunityToolkit.Mvvm.ComponentModel;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>A third-party component's full licence text, read from the app's <c>Licenses/</c> assets.</summary>
    public partial class LicenseViewModel : ObservableObject, IQueryAttributable
    {
        public const string ComponentParameter = "Component";

        private readonly IErrorHandler _errorHandler;

        public LicenseViewModel(IErrorHandler errorHandler)
        {
            _errorHandler = errorHandler;
        }

        [ObservableProperty]
        private ThirdPartyComponent? component;

        [ObservableProperty]
        private string text = string.Empty;

        [ObservableProperty]
        private bool isLoading;

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue(ComponentParameter, out var value) && value is ThirdPartyComponent picked)
                Component = picked;
        }

        public async Task InitializeAsync()
        {
            if (Component?.LicenseFile == null || Text.Length > 0)
                return;

            try
            {
                IsLoading = true;
                var path = $"{ThirdPartyComponent.LicensesFolder}/{Component.LicenseFile}";
                await using var stream = await FileSystem.Current.OpenAppPackageFileAsync(path);
                using var reader = new StreamReader(stream);
                Text = await reader.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToOpenLicense);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
