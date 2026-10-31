using MoneyRecord.Controls;
using MoneyRecord.Helpers;
using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class SettingsPage : ContentPage
    {
        private readonly SettingsViewModel _viewModel;

        public SettingsPage(SettingsViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _viewModel.Refresh();
        }

        /// <summary>Hides the cards whose title doesn't contain the typed text, and the section headers left with no card.</summary>
        private void OnSearchChanged(object? sender, TextChangedEventArgs e)
        {
            var query = (e.NewTextValue ?? string.Empty).Trim();
            Label? header = null;
            var headerHasCard = false;
            foreach (var child in SettingsStack.Children)
            {
                if (child is Label label)
                {
                    if (header != null) header.IsVisible = headerHasCard;
                    header = label;
                    headerHasCard = false;
                }
                else if (child is VerticalStackLayout wrapper && wrapper.Children.FirstOrDefault() is Border card)
                {
                    // A card its own binding hides (no biometrics, no Sentry...) stays hidden whatever is typed
                    var match = ListSearch.Matches(SettingsSearch.GetTitle(wrapper), query);
                    wrapper.IsVisible = match;
                    headerHasCard |= match && card.IsVisible;
                }
            }
            if (header != null) header.IsVisible = headerHasCard;
        }
    }
}
