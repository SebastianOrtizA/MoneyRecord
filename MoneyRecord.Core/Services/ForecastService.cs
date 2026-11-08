using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>
    /// Builds the forecast from today's counted balances, the recurring occurrences (<see cref="IRecurringTransactionService.GetUpcomingAsync"/>,
    /// the same rule that creates them) and the transactions and transfers dated after today. One pass over the rules and two
    /// range queries; nothing is queried per day.
    /// </summary>
    public sealed class ForecastService(
        IAccountRepository accounts,
        IBalanceService balances,
        IRecurringTransactionService recurring,
        IRecurringTransactionRepository recurringRepo,
        ITransactionRepository transactions,
        ITransferRepository transfers,
        IConversionService conversion,
        IExchangeRateService exchangeRates,
        TimeProvider clock) : IForecastService
    {
        public async Task<ForecastResult> GetForecastAsync(int days, CancellationToken cancellationToken = default)
        {
            var today = clock.GetLocalNow().Date;
            var baseCode = MoneyFormatter.BaseCurrency;
            var own = await accounts.GetOwnAsync();
            var currencies = own.ToDictionary(a => a.Id, a => MoneyFormatter.ResolveCurrencyCode(a.CurrencyCode, baseCode));

            var balanceByAccount = (await balances.GetAllAccountBalancesAsync()).ToDictionary(b => b.AccountId, b => Money.ToCents(b.CurrentBalance));
            var start = own.ToDictionary(a => a.Id, a => balanceByAccount.GetValueOrDefault(a.Id));
            cancellationToken.ThrowIfCancellationRequested();

            var events = await GetEventsAsync(today, days, start.Keys.ToHashSet());
            cancellationToken.ThrowIfCancellationRequested();

            var series = BalanceForecast.Build(start, today, days, events);
            var accountForecasts = own.Select(a => new AccountForecast(a.Id, a.Name ?? string.Empty, currencies[a.Id], series[a.Id])).ToList();

            var byCurrency = accountForecasts
                .GroupBy(a => a.CurrencyCode)
                .Select(g => new CurrencyForecast(g.Key, ForecastSeries.Combine(g.Select(a => a.Series).ToList())))
                .OrderBy(c => c.CurrencyCode == baseCode ? 0 : 1)
                .ThenBy(c => c.CurrencyCode, StringComparer.Ordinal)
                .ToList();

            CurrencyForecast? converted = null;
            List<CurrencyAmount> notConverted = [];
            if (byCurrency.Count > 1 && (await conversion.GetStateAsync()).IsConverted)
                (converted, notConverted) = await ConvertAsync(start, currencies, events, today, days, baseCode);

            return new ForecastResult(today, days, accountForecasts, byCurrency, converted, notConverted,
                events.OrderBy(e => e.Date).ThenBy(e => e.Description, StringComparer.CurrentCulture).ToList());
        }

        private async Task<List<ForecastEvent>> GetEventsAsync(DateTime today, int days, HashSet<int> accountIds)
        {
            var events = new List<ForecastEvent>();

            // Recurring occurrences; the debt payments come capped at what is left
            var upcoming = await recurring.GetUpcomingAsync(days);
            if (upcoming.Count > 0)
            {
                var rules = (await recurringRepo.GetActiveAsync()).ToDictionary(r => r.Id);
                foreach (var entry in upcoming)
                {
                    if (!rules.TryGetValue(entry.RecurringId, out var rule) || !accountIds.Contains(rule.AccountId))
                        continue;
                    var cents = Money.ToCents(entry.Amount);
                    events.Add(new ForecastEvent(entry.Date, rule.AccountId, entry.Type == TransactionType.Expense ? -cents : cents,
                        rule.DebtId != null ? ForecastSource.Debt : ForecastSource.Recurring, entry.IsVariable,
                        entry.Description, entry.RecurringId));
                }
            }

            // Entries dated after today are stored but in no balance before their date
            var from = today.AddDays(1);
            var through = today.AddDays(days + 1).AddTicks(-1);
            foreach (var t in await transactions.GetByDateRangeAsync(from, through))
            {
                if (t.AccountId is not { } id || !accountIds.Contains(id) || t.Type == TransactionType.Transfer)
                    continue;
                var cents = Math.Abs(t.AmountCents);
                events.Add(new ForecastEvent(t.Date.Date, id, t.Type == TransactionType.Expense ? -cents : cents,
                    ForecastSource.Scheduled, false, t.Description));
            }

            foreach (var t in await transfers.GetByDateRangeAsync(from, through))
            {
                if (accountIds.Contains(t.SourceAccountId))
                    events.Add(new ForecastEvent(t.Date.Date, t.SourceAccountId, -t.AmountCents, ForecastSource.Scheduled, false, t.Description));
                if (accountIds.Contains(t.DestinationAccountId))
                    events.Add(new ForecastEvent(t.Date.Date, t.DestinationAccountId, t.DestinationAmountCents, ForecastSource.Scheduled, false, t.Description));
            }

            return events;
        }

        /// <summary>
        /// Every account in the base currency as one series: balances at today's rate, each event at its own day's. What has
        /// no rate is left out and returned per currency.
        /// </summary>
        private async Task<(CurrencyForecast, List<CurrencyAmount>)> ConvertAsync(IReadOnlyDictionary<int, long> start,
            IReadOnlyDictionary<int, string> currencies, List<ForecastEvent> events, DateTime today, int days, string baseCode)
        {
            var converter = await exchangeRates.GetConverterAsync();
            var left = new List<CurrencyAmount>();
            var approximate = false;

            long? InBase(long cents, string code, DateTime date)
            {
                if (code == baseCode)
                    return cents;
                if (converter.Convert(Money.FromCents(cents), code, date, baseCode) is not { } amount)
                {
                    left.Add(new CurrencyAmount(code, Money.FromCents(cents)));
                    return null;
                }

                approximate = true;
                return Money.ToCents(amount);
            }

            var total = start.Sum(kv => InBase(kv.Value, currencies[kv.Key], today) ?? 0);
            var convertedEvents = new List<ForecastEvent>();
            foreach (var e in events)
            {
                if (InBase(e.Cents, currencies[e.AccountId], e.Date < today ? today : e.Date) is { } cents)
                    convertedEvents.Add(e with { AccountId = 0, Cents = cents });
            }

            var series = BalanceForecast.Build(new Dictionary<int, long> { [0] = total }, today, days, convertedEvents)[0];
            return (new CurrencyForecast(baseCode, series, approximate), CurrencyAmount.Totals(left, baseCode));
        }
    }
}
