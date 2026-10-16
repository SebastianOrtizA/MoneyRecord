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
        public async Task<bool> CanConvertAsync() =>
            rateSettings.Enabled && (await exchangeRates.GetConverterAsync()).HasRates;

        public async Task<bool> IsConvertedAsync() => display.ShowConverted != false && await CanConvertAsync();

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
            if (!await IsConvertedAsync())
            {
                var byCurrency = PeriodTotals.Build(await balances.GetTotalIncomesAsync(startDate, endDate),
                    await balances.GetTotalExpensesAsync(startDate, endDate), baseCode);
                return new PeriodTotalsResult(byCurrency[0], byCurrency.Skip(1).ToList(), IsConverted: false);
            }

            var result = await ConvertAsync(await transactions.GetByDateRangeAsync(startDate, endDate), await accounts.GetAllAsync());
            var converted = result.Converted;
            var total = new PeriodTotals(baseCode, Sum(converted, TransactionType.Income), Sum(converted, TransactionType.Expense))
            {
                // "≈" only when something was actually converted
                IsApproximate = result.FromOtherCurrencies > 0
            };
            // Build always puts the base first (with zeros here: base entries never lack a rate)
            var others = PeriodTotals.Build(Amounts(result.NotConverted, TransactionType.Income),
                Amounts(result.NotConverted, TransactionType.Expense), baseCode).Skip(1).ToList();
            return new PeriodTotalsResult(total, others, IsConverted: true);
        }

        public async Task<Dictionary<int, BudgetSpent>> GetExpensesByCategoryAsync(DateTime startDate, DateTime endDate)
        {
            var baseCode = MoneyFormatter.BaseCurrency;
            if (!await IsConvertedAsync())
            {
                return (await balances.GetExpensesByCategoryAsync(startDate, endDate))
                    .ToDictionary(kv => kv.Key, kv => BudgetSpent.FromCurrencies(kv.Value, baseCode));
            }

            var expenses = (await transactions.GetByDateRangeAsync(startDate, endDate)).Where(t => t.Type == TransactionType.Expense);
            var result = await ConvertAsync(expenses, await accounts.GetAllAsync());
            var counted = result.Converted.GroupBy(t => t.CategoryId).ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
            var uncounted = result.NotConverted.GroupBy(t => t.CategoryId).ToDictionary(g => g.Key,
                g => CurrencyAmount.Totals(g.Select(t => new CurrencyAmount(t.CurrencyCode, t.Amount)), baseCode));

            return counted.Keys.Union(uncounted.Keys).ToDictionary(id => id,
                id => new BudgetSpent(counted.GetValueOrDefault(id), uncounted.GetValueOrDefault(id) ?? []));
        }

        private static decimal Sum(IEnumerable<Transaction> converted, TransactionType type) =>
            converted.Where(t => t.Type == type).Sum(t => t.Amount);

        private static IEnumerable<CurrencyAmount> Amounts(IEnumerable<Transaction> notConverted, TransactionType type) =>
            notConverted.Where(t => t.Type == type).Select(t => new CurrencyAmount(t.CurrencyCode, t.Amount));
    }
}
