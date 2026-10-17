using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>
    /// Settings → Base currency → Show cents: one switch per currency in use (the base, then every account's) that has
    /// cents. Off shows its amounts in whole units; they are still stored to the cent.
    /// </summary>
    public partial class CurrencyCentsViewModel(IAccountRepository accounts, ICurrencyService currencies) : ObservableObject
    {
        [ObservableProperty]
        private IReadOnlyList<CurrencyCentsOption> currencyOptions = [];

        [RelayCommand]
        private async Task RefreshAsync()
        {
            var codes = ReportCurrencies.InUse(await accounts.GetAllAsync(), currencies.GetBaseCurrencyCode());
            CurrencyOptions = codes
                .Where(CurrencyService.HasCents)
                .Select(code => new CurrencyCentsOption(code, currencies))
                .ToList();
        }
    }

    /// <summary>One currency's switch, with a sample amount that shows the choice.</summary>
    public partial class CurrencyCentsOption : ObservableObject
    {
        /// <summary>The amount the sample shows ("$1.234,56" or "$1.235").</summary>
        public const decimal SampleAmount = 1234.56m;

        private readonly ICurrencyService _currencies;

        public CurrencyCentsOption(string code, ICurrencyService currencies)
        {
            _currencies = currencies;
            Code = code;
            Name = currencies.GetCurrency(code)?.Name ?? code;
            showsCents = currencies.ShowsCents(code);
        }

        public string Code { get; }

        public string Name { get; }

        public string Sample => MoneyFormatter.FormatBesideCode(SampleAmount, Code);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Sample))]
        private bool showsCents;

        partial void OnShowsCentsChanged(bool value) => _currencies.SetShowsCents(Code, value);
    }
}
