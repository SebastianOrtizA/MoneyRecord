using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Views;

namespace MoneyRecord.Controls
{
    /// <summary>"More reports ›" at the top of a Reports tab: pushes the reports hub (back returns to the tab).</summary>
    public class MoreReportsLink : Label
    {
        public MoreReportsLink()
        {
            FontSize = 14;
            FontAttributes = FontAttributes.Bold;
            HorizontalOptions = LayoutOptions.End;
            MinimumHeightRequest = 32;
            VerticalTextAlignment = TextAlignment.Center;
            ThemeColors.Apply(this, "Primary", "PrimaryDark");
            Text = LocalizationService.Instance.GetString("MoreReports") + " ›";
            SemanticProperties.SetHint(this, LocalizationService.Instance.GetString("MoreReports"));

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                if (Handler?.MauiContext?.Services.GetService<INavigationService>() is { } navigation)
                    await navigation.NavigateToAsync(nameof(ReportsHubPage));
            };
            GestureRecognizers.Add(tap);
            LocalizationService.Instance.LanguageChanged += (_, _) => Text = LocalizationService.Instance.GetString("MoreReports") + " ›";
        }
    }
}
