using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    public interface ICurrencyService
    {
        List<Currency> GetAvailableCurrencies();
        Currency? GetCurrency(string code);
        string GetSymbol(string code);
        string GetDefaultCurrencyCode();
        string FormatAmount(decimal amount, string currencyCode);
    }
}
