using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;
using SQLite;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>PocketService against a real temp SQLite file (balances are SQL totals).</summary>
public sealed class PocketServiceTests : IAsyncLifetime
{
    private static readonly DateTime Today = new(2026, 10, 16);

    private readonly string _appData = Path.Combine(Path.GetTempPath(), $"moneyrecord-pockets-{Guid.NewGuid():N}");
    private DatabaseInitializer _dbInitializer = null!;
    private AccountRepository _accounts = null!;
    private PocketService _sut = null!;

    private SQLiteAsyncConnection Db { get; set; } = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_appData);
        _dbInitializer = new DatabaseInitializer(new AppPaths(_appData));
        Db = await _dbInitializer.GetConnectionAsync();
        _accounts = new AccountRepository(_dbInitializer);
        var clock = new FakeTimeProvider(new DateTimeOffset(Today.AddHours(12), TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        var balances = new BalanceService(_accounts, new TransactionRepository(_dbInitializer), new TransferRepository(_dbInitializer), clock);
        _sut = new PocketService(_dbInitializer, balances);
    }

    public async Task DisposeAsync()
    {
        await Db.CloseAsync();
        Directory.Delete(_appData, recursive: true);
    }

    private async Task<Account> AddAccountAsync(string name, decimal initial, bool isDebt = false)
    {
        var account = new Account { Name = name, InitialBalance = initial, CurrencyCode = MoneyFormatter.BaseCurrency, IsDebt = isDebt };
        await _accounts.SaveAsync(account);
        return account;
    }

    private async Task<Pocket> AddPocketAsync(Account account, string name = "Travel")
    {
        var pocket = new Pocket { AccountId = account.Id, Name = name };
        await _sut.SaveAsync(pocket);
        return pocket;
    }

    [Fact]
    public async Task Setting_aside_and_taking_out_move_the_pocket_balance_only()
    {
        var bank = await AddAccountAsync("Bank", 2000m);
        var pocket = await AddPocketAsync(bank);

        await _sut.MoveAsync(pocket.Id, 500m, Today);
        await _sut.MoveAsync(pocket.Id, -200m, Today);

        (await _sut.GetBalancesAsync())[pocket.Id].Should().Be(30000);
        (await _sut.GetTotalsByAccountAsync())[bank.Id].Should().Be(30000);
        var summary = (await _sut.GetPocketsAsync(bank.Id)).Single();
        summary.Balance.Should().Be(300m);
        summary.DisplayName.Should().Be("Bank › Travel");
        (await _sut.GetMovementsAsync(pocket.Id)).Should().HaveCount(2);
    }

    [Fact]
    public async Task Setting_aside_more_than_available_is_refused()
    {
        var bank = await AddAccountAsync("Bank", 1000m);
        var travel = await AddPocketAsync(bank);
        var rainy = await AddPocketAsync(bank, "Rainy day");
        await _sut.MoveAsync(travel.Id, 800m, Today);

        var act = () => _sut.MoveAsync(rainy.Id, 300m, Today);

        (await act.Should().ThrowAsync<PocketProblemException>()).Which.Problem.Should().Be(PocketProblem.NotEnoughAvailable);
        (await _sut.GetMovementsAsync(rainy.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Taking_out_more_than_the_pocket_holds_is_refused()
    {
        var bank = await AddAccountAsync("Bank", 1000m);
        var pocket = await AddPocketAsync(bank);
        await _sut.MoveAsync(pocket.Id, 100m, Today);

        var act = () => _sut.MoveAsync(pocket.Id, -150m, Today);

        (await act.Should().ThrowAsync<PocketProblemException>()).Which.Problem.Should().Be(PocketProblem.NotEnoughInPocket);
    }

    [Fact]
    public async Task A_debts_hidden_account_cannot_have_pockets()
    {
        var ledger = await AddAccountAsync("Ana", 100m, isDebt: true);

        var act = () => _sut.SaveAsync(new Pocket { AccountId = ledger.Id, Name = "No" });

        (await act.Should().ThrowAsync<PocketProblemException>()).Which.Problem.Should().Be(PocketProblem.DebtAccount);
    }

    [Fact]
    public async Task Shortfall_is_what_the_pockets_exceed_the_balance_by_after_the_outflow()
    {
        var bank = await AddAccountAsync("Bank", 2000m);
        var pocket = await AddPocketAsync(bank);
        await _sut.MoveAsync(pocket.Id, 500m, Today);

        (await _sut.GetShortfallAsync(bank.Id, 1500m, Today)).Should().Be(0m, "1,500 is exactly what is available");
        (await _sut.GetShortfallAsync(bank.Id, 1800m, Today)).Should().Be(300m);
        (await _sut.GetShortfallAsync(bank.Id, 1800m, Today.AddDays(5))).Should().Be(0m, "a future-dated entry is in no balance yet");
    }

    [Fact]
    public async Task Cover_takes_the_money_out_linked_to_the_entry()
    {
        var bank = await AddAccountAsync("Bank", 2000m);
        var pocket = await AddPocketAsync(bank);
        await _sut.MoveAsync(pocket.Id, 500m, Today);

        await _sut.CoverAsync([new PocketTake(pocket.Id, 300m)], Today, transactionId: 7);

        (await _sut.GetBalancesAsync())[pocket.Id].Should().Be(20000);
        var last = (await _sut.GetMovementsAsync(pocket.Id)).First();
        last.AmountCents.Should().Be(-30000);
        last.TransactionId.Should().Be(7);

        var tooMuch = () => _sut.CoverAsync([new PocketTake(pocket.Id, 300m)], Today);
        await tooMuch.Should().ThrowAsync<PocketProblemException>();
    }

    [Fact]
    public async Task Deleting_a_pocket_removes_its_movements_and_goal_link_and_frees_the_money()
    {
        var bank = await AddAccountAsync("Bank", 2000m);
        var pocket = await AddPocketAsync(bank);
        await _sut.MoveAsync(pocket.Id, 500m, Today);
        await Db.InsertAsync(new SavingsGoalPocket { GoalId = 1, PocketId = pocket.Id });

        await _sut.DeleteAsync(pocket.Id);

        (await Db.Table<Pocket>().CountAsync()).Should().Be(0);
        (await Db.Table<PocketMovement>().CountAsync()).Should().Be(0);
        (await Db.Table<SavingsGoalPocket>().CountAsync()).Should().Be(0);
        (await _sut.GetTotalsByAccountAsync()).Should().BeEmpty();
    }
}
