using MoneyRecord.Helpers;
using MoneyRecord.Models;

namespace MoneyRecord.Views
{
    /// <summary>
    /// A modal page listing currencies under a search box. <see cref="Result"/> completes with the tapped currency, or
    /// null when cancelled (Cancel or Android back).
    /// </summary>
    public partial class CurrencyPickerPage : ContentPage
    {
        private readonly IReadOnlyList<Currency> _all;
        private readonly TaskCompletionSource<Currency?> _result = new();

        public CurrencyPickerPage(IReadOnlyList<Currency> currencies)
        {
            InitializeComponent();
            _all = currencies;
            Results.ItemsSource = _all;
        }

        public Task<Currency?> Result => _result.Task;

        private void OnSearchChanged(object? sender, TextChangedEventArgs e) =>
            Results.ItemsSource = CurrencySearch.Filter(_all, e.NewTextValue);

        private async void OnCurrencyTapped(object? sender, TappedEventArgs e)
        {
            if (sender is BindableObject { BindingContext: Currency currency })
                await CloseAsync(currency);
        }

        private async void OnCancelClicked(object? sender, EventArgs e) => await CloseAsync(null);

        /// <summary>On Android a modal page gets back before the Shell does: back cancels.</summary>
        protected override bool OnBackButtonPressed()
        {
            _ = CloseAsync(null);
            return true;
        }

        private async Task CloseAsync(Currency? currency)
        {
            if (_result.Task.IsCompleted)
                return;
            SearchBox.Unfocus();
            await Navigation.PopModalAsync(animated: false);
            _result.TrySetResult(currency);
        }
    }
}
