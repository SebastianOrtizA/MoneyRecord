using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    public interface ICurrencyService
    {
        List<Currency> GetAvailableCurrencies();
        Currency? GetCurrency(string code);
        string GetSymbol(string code);
        string GetDefaultCurrencyCode();

        /// <summary>The base currency (Settings): totals and reports are shown in it. Default: the device region's.</summary>
        string GetBaseCurrencyCode();
        void SetBaseCurrencyCode(string code);

        /// <summary>Amounts in the currency show cents (Settings; on by default, never for JPY, CLP or KRW).</summary>
        bool ShowsCents(string code);
        void SetShowsCents(string code, bool show);
        string FormatAmount(decimal amount, string currencyCode);
    }
}
