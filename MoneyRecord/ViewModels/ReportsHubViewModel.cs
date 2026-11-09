using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Views;

namespace MoneyRecord.ViewModels
{
    /// <summary>A report of the hub: icon glyph, title, one line about it and the pushed route.</summary>
    public record HubReport(string Icon, string Title, string Hint, string Route);

    /// <summary>The "More reports" list (pushed from the Reports tabs and More): every report that has no tab of its own.</summary>
    public partial class ReportsHubViewModel : ObservableObject
    {
        private readonly INavigationService _navigationService;

        [ObservableProperty]
        private IReadOnlyList<HubReport> reports = [];

        public ReportsHubViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService;
            Reports = Build();
            // A language change while the page is open keeps the titles right
            LocalizationService.Instance.LanguageChanged += (_, _) => Reports = Build();
        }

        private static IReadOnlyList<HubReport> Build() =>
        [
            new("\U000F0127", AppResources.CategoryTrend, AppResources.CategoryTrendHint, nameof(CategoryTrendPage)),
            new("\U000F04DC", AppResources.PayeeReport, AppResources.PayeeReportHint, nameof(PayeeReportPage)),
            new("\U000F04F9", AppResources.Tags, AppResources.TagsHint, nameof(TagsPage)),
            new("\U000F0070", AppResources.AccountReport, AppResources.AccountReportHint, nameof(AccountReportPage)),
            new("\U000F1492", AppResources.CompareReport, AppResources.CompareReportHint, nameof(ComparePeriodsPage))
        ];

        [RelayCommand]
        private Task OpenAsync(HubReport report) => _navigationService.NavigateToAsync(report.Route);
    }
}
