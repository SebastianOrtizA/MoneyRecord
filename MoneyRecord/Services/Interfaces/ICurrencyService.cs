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
        string FormatAmount(decimal amount, string currencyCode);
    }
}
