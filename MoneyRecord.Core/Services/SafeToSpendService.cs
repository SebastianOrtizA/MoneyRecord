using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>
    /// Safe to spend from the 30-day forecast of the first currency (base first): the balance the accounts hold today minus the
    /// committed outflows up to the chosen horizon. The month end and the next income both fall within 30 days.
    /// </summary>
    public sealed class SafeToSpendService(IForecastService forecast, IPlanningSettings settings) : ISafeToSpendService
    {
        public async Task<HomeForecast?> GetAsync(CancellationToken cancellationToken = default)
        {
            var result = await forecast.GetForecastAsync(30, cancellationToken);
            if (result.Currencies.FirstOrDefault() is not { } primary)
                return null;

            var ids = result.Accounts.Where(a => a.CurrencyCode == primary.CurrencyCode).Select(a => a.AccountId).ToHashSet();
            var events = result.Events.Where(e => ids.Contains(e.AccountId)).ToList();

            // The first point already holds what is due today or overdue: take it out to get what the accounts hold now
            var dueToday = events.Where(e => e.Date.Date <= result.Today).Sum(e => e.Cents);
            var available = primary.Series.Points[0].Cents - dueToday;

            var safe = SafeToSpend.Compute(available, events, result.Today, settings.SafeToSpendUntil);
            return new HomeForecast(primary.CurrencyCode, safe, primary.Series.FirstNegativeDate);
        }
    }
}
