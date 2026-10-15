using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Repositories;
using SQLite;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>
/// BalanceService against a real temp SQLite file: the totals are SQL (SUM ... GROUP BY), so only a database
/// can check them. The seed data adds the default Cash account with no transactions.
/// </summary>
public sealed class BalanceServiceTests : IAsyncLifetime
{
    private static readonly DateTime March1 = new(2026, 3, 1);
    private static readonly DateTime March31End = new DateTime(2026, 4, 1).AddTicks(-1);

    private readonly string _appData = Path.Combine(Path.GetTempPath(), $"moneyrecord-balance-{Guid.NewGuid():N}");
    private DatabaseInitializer _dbInitializer = null!;
    private BalanceService _sut = null!;

    private SQLiteAsyncConnection Db { get; set; } = null!;

    private static string Base => MoneyFormatter.ResolveCurrencyCode(null);

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_appData);
        _dbInitializer = new DatabaseInitializer(new AppPaths(_appData));
        Db = await _dbInitializer.GetConnectionAsync();
        _sut = new BalanceService(
            new AccountRepository(_dbInitializer),
            new TransactionRepository(_dbInitializer),
            new TransferRepository(_dbInitializer));
    }

    public async Task DisposeAsync()
    {
        await Db.CloseAsync();
        Directory.Delete(_appData, recursive: true);
    }

    private async Task<Account> AddAccountAsync(string name, decimal initial = 0, DateTime? created = null, string currencyCode = "")
    {
        var account = new Account
        {
            Name = name, InitialBalance = initial, CreatedDate = created ?? new DateTime(2026, 1, 1), CurrencyCode = currencyCode
        };
        await Db.InsertAsync(account);
        return account;
    }

    private Task AddAsync(TransactionType type, decimal amount, DateTime date, int? accountId = null, int categoryId = 1) =>
        Db.InsertAsync(new Transaction { Type = type, Amount = amount, Date = date, AccountId = accountId, CategoryId = categoryId });

    private Task TransferAsync(int from, int to, decimal amount, DateTime date) =>
        Db.InsertAsync(new Transfer { SourceAccountId = from, DestinationAccountId = to, Amount = amount, DestinationAmount = amount, Date = date });

    [Fact]
    public async Task Account_balance_is_initial_plus_incomes_minus_expenses_and_transfers()
    {
        var bank = await AddAccountAsync("Bank", initial: 500m);
        var savings = await AddAccountAsync("Savings", initial: 100m);
        await AddAsync(TransactionType.Income, 200m, March1, bank.Id);
        await AddAsync(TransactionType.Expense, 50.25m, March1, bank.Id);
        await AddAsync(TransactionType.Expense, -30m, March1, bank.Id); // stored negative by an old version: still an expense
        await TransferAsync(bank.Id, savings.Id, 120m, March1);

        var balances = (await _sut.GetAllAccountBalancesAsync()).ToDictionary(b => b.AccountName);

        balances["Bank"].CurrentBalance.Should().Be(500m + 200m - 50.25m - 30m - 120m);
        balances["Savings"].CurrentBalance.Should().Be(220m);
        (await _sut.GetAccountBalanceAsync(savings.Id)).Should().Be(220m);
    }

    [Fact]
    public async Task A_cross_currency_transfer_takes_what_left_and_adds_what_arrived()
    {
        var pesos = await AddAccountAsync("Pesos", initial: 1_000_000m);
        var dollars = await AddAccountAsync("Dollars");
        await Db.InsertAsync(new Transfer
        {
            SourceAccountId = pesos.Id, DestinationAccountId = dollars.Id, Amount = 401_250m, DestinationAmount = 100m, Date = March1
        });

        var balances = (await _sut.GetAllAccountBalancesAsync()).ToDictionary(b => b.AccountName);

        balances["Pesos"].CurrentBalance.Should().Be(598_750m);
        balances["Dollars"].CurrentBalance.Should().Be(100m);
    }

    [Fact]
    public async Task An_unknown_account_has_no_balance()
    {
        (await _sut.GetAccountBalanceAsync(999)).Should().Be(0);
    }

    [Fact]
    public async Task Transactions_without_an_account_are_in_no_account_balance()
    {
        var bank = await AddAccountAsync("Bank", initial: 10m);
        await AddAsync(TransactionType.Income, 99m, March1, accountId: null);

        (await _sut.GetAccountBalanceAsync(bank.Id)).Should().Be(10m);
    }

    [Fact]
    public async Task Last_activity_is_the_latest_transaction_or_transfer_else_the_creation_date()
    {
        var created = new DateTime(2025, 6, 1);
        var bank = await AddAccountAsync("Bank", created: created);
        var savings = await AddAccountAsync("Savings", created: created);
        var idle = await AddAccountAsync("Idle", created: created);
        await AddAsync(TransactionType.Expense, 1m, new DateTime(2026, 2, 10, 9, 30, 0), bank.Id);
        await TransferAsync(bank.Id, savings.Id, 1m, new DateTime(2026, 3, 5, 18, 0, 0));

        var balances = (await _sut.GetAllAccountBalancesAsync()).ToDictionary(b => b.AccountId);

        balances[bank.Id].LastActivityDate.Should().Be(new DateTime(2026, 3, 5, 18, 0, 0));
        balances[savings.Id].LastActivityDate.Should().Be(new DateTime(2026, 3, 5, 18, 0, 0));
        balances[idle.Id].LastActivityDate.Should().Be(created);
    }

    [Fact]
    public async Task Period_totals_include_both_ends_of_the_range_and_only_their_type()
    {
        await AddAsync(TransactionType.Income, 100m, March1);
        await AddAsync(TransactionType.Income, 50m, March31End);
        await AddAsync(TransactionType.Income, 7m, March31End.AddTicks(1)); // April
        await AddAsync(TransactionType.Expense, 30m, March1.AddDays(3));

        (await _sut.GetTotalIncomesAsync(March1, March31End)).Should().Equal(new CurrencyAmount(Base, 150m));
        (await _sut.GetTotalExpensesAsync(March1, March31End)).Should().Equal(new CurrencyAmount(Base, 30m));
    }

    [Fact]
    public async Task An_empty_period_totals_zero()
    {
        (await _sut.GetTotalExpensesAsync(March1, March31End)).Should().BeEmpty();
    }

    [Fact]
    public async Task Category_expenses_count_only_that_category_and_type()
    {
        await AddAsync(TransactionType.Expense, 12.5m, March1, categoryId: 5);
        await AddAsync(TransactionType.Expense, 7.5m, March1.AddDays(1), categoryId: 5);
        await AddAsync(TransactionType.Expense, 100m, March1, categoryId: 6);
        await AddAsync(TransactionType.Income, 40m, March1, categoryId: 5);

        (await _sut.GetCategoryExpensesAsync(5, March1, March31End)).Should().Equal(new CurrencyAmount(Base, 20m));

        var byCategory = await _sut.GetExpensesByCategoryAsync(March1, March31End);
        byCategory.Keys.Should().BeEquivalentTo([5, 6]);
        byCategory[5].Should().Equal(new CurrencyAmount(Base, 20m));
        byCategory[6].Should().Equal(new CurrencyAmount(Base, 100m));
    }

    [Fact]
    public async Task Period_totals_are_per_currency_base_first_never_added_together()
    {
        var previousBase = MoneyFormatter.BaseCurrencyCode;
        MoneyFormatter.BaseCurrencyCode = "COP"; // not EUR or USD, whatever the machine's region
        try
        {
            var euros = await AddAccountAsync("Euros", currencyCode: "EUR");
            var dollars = await AddAccountAsync("Dollars", currencyCode: "USD");
            await AddAsync(TransactionType.Expense, 10m, March1, dollars.Id, categoryId: 5);
            await AddAsync(TransactionType.Expense, 20m, March1, euros.Id, categoryId: 5);
            await AddAsync(TransactionType.Expense, 30m, March1, euros.Id, categoryId: 6);
            await AddAsync(TransactionType.Expense, 40m, March1, categoryId: 5); // no account: the base currency

            var totals = await _sut.GetTotalExpensesAsync(March1, March31End);
            var byCategory = await _sut.GetExpensesByCategoryAsync(March1, March31End);

            totals[0].Should().Be(new CurrencyAmount("COP", 40m));
            totals.Skip(1).Should().Equal(new CurrencyAmount("EUR", 50m), new CurrencyAmount("USD", 10m));
            byCategory[5].Should().Equal(new CurrencyAmount("COP", 40m), new CurrencyAmount("EUR", 20m), new CurrencyAmount("USD", 10m));
            byCategory[6].Should().Equal(new CurrencyAmount("EUR", 30m));
        }
        finally
        {
            MoneyFormatter.BaseCurrencyCode = previousBase;
        }
    }
}
