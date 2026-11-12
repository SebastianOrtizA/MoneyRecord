using MoneyRecord.Helpers;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Views
{
    /// <summary>
    /// A modal page asking at what rate an account's amounts become the default account's currency. The rate and the
    /// final amount (what the net total of the transactions is worth) are linked: the one being typed fills the other.
    /// <see cref="Result"/> completes with the rate, or null when cancelled (Cancel or Android back).
    /// </summary>
    public partial class ConvertAccountPage : ContentPage
    {
        private readonly AccountConversionRequest _request;
        private readonly TaskCompletionSource<decimal?> _result = new();
        private bool _filling;

        public ConvertAccountPage(AccountConversionRequest request)
        {
            InitializeComponent();
            _request = request;

            var from = MoneyFormatter.ResolveCurrencyCode(request.FromCurrency);
            var to = MoneyFormatter.ResolveCurrencyCode(request.ToCurrency);
            MessageLabel.Text = string.Format(AppResources.ConvertAccountMessage,
                request.AccountName, from, request.DefaultAccountName, to);
            RateCaption.Text = AppResources.ConvertAccountRate;
            RateFromLabel.Text = $"1 {from} =";
            RateToLabel.Text = to;

            // With nothing to price (no transactions, or they add up to zero) only the rate can be typed
            var hasNet = request.NetCents != 0;
            NetLabel.IsVisible = hasNet;
            NetLabel.Text = string.Format(AppResources.ConvertAccountNet, MoneyFormatter.Format(Money.FromCents(request.NetCents), from));
            FinalBox.IsVisible = hasNet;
            NoNetLabel.IsVisible = !hasNet;
            FinalCaption.Text = string.Format(AppResources.ConvertAccountFinal, to);

            if (request.SuggestedRate is > 0)
                RateEntry.Text = AccountMoveRate.FormatRate(request.SuggestedRate.Value);
        }

        public Task<decimal?> Result => _result.Task;

        protected override void OnAppearing()
        {
            base.OnAppearing();
            Dispatcher.Dispatch(() => RateEntry.Focus());
        }

        private decimal? Rate => AccountMoveRate.ParseRate(RateEntry.Text);

        /// <summary>The rate is being typed (or filled in): the final amount follows it.</summary>
        private void OnRateChanged(object? sender, TextChangedEventArgs e)
        {
            ConfirmButton.IsEnabled = Rate != null;
            if (_filling || _request.NetCents == 0)
                return;

            Fill(() => FinalEntry.Text = Rate is { } rate
                ? MoneyFormatter.FormatForEntry(AccountMoveRate.FinalFor(_request.NetCents, rate), _request.ToCurrency)
                : string.Empty);
        }

        /// <summary>The final amount is being typed: the rate follows it.</summary>
        private void OnFinalChanged(object? sender, TextChangedEventArgs e)
        {
            if (_filling)
                return;

            var final = AmountParser.Parse(FinalEntry.Text, _request.ToCurrency);
            Fill(() => RateEntry.Text = AccountMoveRate.RateFor(_request.NetCents, final) is { } rate
                ? AccountMoveRate.FormatRate(rate)
                : string.Empty);
        }

        // Filling one box must not make the other fill this one back
        private void Fill(Action fill)
        {
            _filling = true;
            try
            {
                fill();
            }
            finally
            {
                _filling = false;
            }
        }

        private async void OnConfirmClicked(object? sender, EventArgs e)
        {
            if (Rate is { } rate)
                await CloseAsync(rate);
        }

        private async void OnCancelClicked(object? sender, EventArgs e) => await CloseAsync(null);

        /// <summary>On Android a modal page gets back before the Shell does: back cancels.</summary>
        protected override bool OnBackButtonPressed()
        {
            _ = CloseAsync(null);
            return true;
        }

        private async Task CloseAsync(decimal? rate)
        {
            if (_result.Task.IsCompleted)
                return;
            RateEntry.Unfocus();
            FinalEntry.Unfocus();
            await Navigation.PopModalAsync(animated: false);
            _result.TrySetResult(rate);
        }
    }
}
