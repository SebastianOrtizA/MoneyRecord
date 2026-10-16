using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class ConversionService(
        IExchangeRateService exchangeRates,
        IExchangeRateSettings rateSettings,
        ICurrencyDisplaySettings display,
        IAccountRepository accounts,
        ITransactionRepository transactions,
        IBalanceService balances) : IConversionService
    {
        public async Task<ConversionState> GetStateAsync()
        {
            var canConvert = rateSettings.Enabled
                && ReportCurrencies.InUse(await accounts.GetAllAsync(), MoneyFormatter.BaseCurrency).Count > 1
                && (await exchangeRates.GetConverterAsync()).HasRates;
            return new ConversionState(canConvert, canConvert && display.ShowConverted != false);
        }

        public void SetConverted(bool converted) => display.ShowConverted = converted;

        public async Task<ConversionResult> ConvertAsync(IEnumerable<Transaction> transactions, IEnumerable<Account> accounts)
        {
            var converter = await exchangeRates.GetConverterAsync();
            var baseCode = MoneyFormatter.BaseCurrency;
            var codes = accounts.ToDictionary(a => a.Id, a => MoneyFormatter.ResolveCurrencyCode(a.CurrencyCode, baseCode));

            var converted = new List<Transaction>();
            var notConverted = new List<Transaction>();
            var fromOtherCurrencies = 0;
            foreach (var transaction in transactions)
            {
                // The account's currency; no account (or a deleted one) = the base, like everywhere else
                var code = transaction.AccountId is { } id && codes.TryGetValue(id, out var accountCode) ? accountCode : baseCode;
                if (code == baseCode)
                    converted.Add(transaction.WithAmount(transaction.Amount, baseCode));
                else if (converter.Convert(transaction, code, baseCode) is { } amount)
                {
                    converted.Add(transaction.WithAmount(amount, baseCode));
                    fromOtherCurrencies++;
                }
                else
                    notConverted.Add(transaction.WithAmount(transaction.Amount, code));
            }

            return new ConversionResult(converted, notConverted, fromOtherCurrencies);
        }

        public async Task<ReportTransactions> ForReportAsync(IEnumerable<Transaction> transactions,
            IReadOnlyCollection<Account> accounts, bool converted, string currencyCode)
        {
            var baseCode = MoneyFormatter.BaseCurrency;
            if (!converted)
            {
                var inCurrency = ReportCurrencies.Only(transactions, accounts, currencyCode, baseCode)
                    .Select(t => t.WithAmount(t.Amount, currencyCode)).ToList();
                return new ReportTransactions(inCurrency, currencyCode, [], IsApproximate: false);
            }

            var result = await ConvertAsync(transactions, accounts);
            return new ReportTransactions(result.Converted, baseCode, result.NotConvertedTotals(baseCode),
                IsApproximate: result.FromOtherCurrencies > 0);
        }

        public async Task<PeriodTotalsResult> GetPeriodTotalsAsync(DateTime startDate, DateTime endDate)
        {
            var baseCode = MoneyFormatter.BaseCurrency;
            if (!(await GetStateAsync()).IsConverted)
            {
                var byCurrency = PeriodTotals.Build(await balances.GetTotalIncomesAsync(startDate, endDate),
                    await balances.GetTotalExpensesAsync(startDate, endDate), baseCode);
                return new PeriodTotalsResult(byCurrency[0], byCurrency.Skip(1).ToList(), IsConverted: false);
            }

            var (converted, notConverted, approximate) = await ConvertDailyTotalsAsync(startDate, endDate);
            var total = new PeriodTotals(baseCode, Sum(converted, TransactionType.Income), Sum(converted, TransactionType.Expense))
            {
                IsApproximate = approximate
            };
            // Build always puts the base first (with zeros here: base amounts never lack a rate)
            var others = PeriodTotals.Build(Amounts(notConverted, TransactionType.Income),
                Amounts(notConverted, TransactionType.Expense), baseCode).Skip(1).ToList();
            return new PeriodTotalsResult(total, others, IsConverted: true);
        }

        public async Task<Dictionary<int, BudgetSpent>> GetExpensesByCategoryAsync(DateTime startDate, DateTime endDate)
        {
            var baseCode = MoneyFormatter.BaseCurrency;
            if (!(await GetStateAsync()).IsConverted)
            {
                return (await balances.GetExpensesByCategoryAsync(startDate, endDate))
                    .ToDictionary(kv => kv.Key, kv => BudgetSpent.FromCurrencies(kv.Value, baseCode));
            }

            var (converted, notConverted, _) = await ConvertDailyTotalsAsync(startDate, endDate);
            var counted = converted.Where(c => c.Row.Type == TransactionType.Expense)
                .GroupBy(c => c.Row.CategoryId).ToDictionary(g => g.Key, g => g.Sum(c => c.Amount));
            var uncounted = notConverted.Where(r => r.Type == TransactionType.Expense).GroupBy(r => r.CategoryId)
                .ToDictionary(g => g.Key, g => CurrencyAmount.Totals(g.Select(r => new CurrencyAmount(r.CurrencyCode, r.Amount)), baseCode));

            return counted.Keys.Union(uncounted.Keys).ToDictionary(id => id,
                id => new BudgetSpent(counted.GetValueOrDefault(id), uncounted.GetValueOrDefault(id) ?? []));
        }

        /// <summary>
        /// The range's SQL day totals (<see cref="ITransactionRepository.SumByDayAsync"/>) in the base currency: each day's
        /// sum converted at that day's rate (no row is loaded per entry); the ones without a rate are returned as they are.
        /// </summary>
        private async Task<(List<(DailyTotal Row, decimal Amount)> Converted, List<DailyTotal> NotConverted, bool Approximate)>
            ConvertDailyTotalsAsync(DateTime startDate, DateTime endDate)
        {
            var baseCode = MoneyFormatter.BaseCurrency;
            var converter = await exchangeRates.GetConverterAsync();
            var converted = new List<(DailyTotal, decimal)>();
            var notConverted = new List<DailyTotal>();
            var approximate = false;
            foreach (var row in await transactions.SumByDayAsync(startDate, endDate, baseCode))
            {
                if (row.IsOwnConversion || MoneyFormatter.SameCurrency(row.CurrencyCode, baseCode))
                {
                    converted.Add((row, row.Amount));
                    approximate |= row.IsOwnConversion;
                }
                else if (converter.Convert(row.Amount, row.CurrencyCode, row.Date, baseCode) is { } amount)
                {
                    converted.Add((row, amount));
                    approximate = true;
                }
                else
                {
                    notConverted.Add(row);
                }
            }

            return (converted, notConverted, approximate);
        }

        private static decimal Sum(IEnumerable<(DailyTotal Row, decimal Amount)> converted, TransactionType type) =>
            converted.Where(c => c.Row.Type == type).Sum(c => c.Amount);

        private static IEnumerable<CurrencyAmount> Amounts(IEnumerable<DailyTotal> notConverted, TransactionType type) =>
            notConverted.Where(r => r.Type == type).Select(r => new CurrencyAmount(r.CurrencyCode, r.Amount));
    }
}
