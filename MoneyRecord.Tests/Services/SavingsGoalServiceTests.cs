using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Repositories;
using SQLite;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>SavingsGoalService against a real temp SQLite file (balances and the pace are SQL totals).</summary>
public sealed class SavingsGoalServiceTests : IAsyncLifetime
{
    private static readonly DateTime Today = new(2026, 10, 16);

    private readonly string _appData = Path.Combine(Path.GetTempPath(), $"moneyrecord-goals-{Guid.NewGuid():N}");
    private DatabaseInitializer _dbInitializer = null!;
    private AccountRepository _accounts = null!;
    private SavingsGoalService _sut = null!;

    private SQLiteAsyncConnection Db { get; set; } = null!;

    private static string Base => MoneyFormatter.BaseCurrency;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_appData);
        _dbInitializer = new DatabaseInitializer(new AppPaths(_appData));
        Db = await _dbInitializer.GetConnectionAsync();
        _accounts = new AccountRepository(_dbInitializer);
        var clock = new FakeTimeProvider(new DateTimeOffset(Today.AddHours(12), TimeSpan.Zero));
        var balances = new BalanceService(_accounts, new TransactionRepository(_dbInitializer), new TransferRepository(_dbInitializer), clock);
        clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        _sut = new SavingsGoalService(_dbInitializer, _accounts, balances, clock);
    }

    public async Task DisposeAsync()
    {
        await Db.CloseAsync();
        Directory.Delete(_appData, recursive: true);
    }

    // Through the repository: it caches the accounts
    private async Task<Account> AddAccountAsync(string name, decimal initial = 0, string? currencyCode = null, DateTime? created = null)
    {
        var account = new Account
        {
            Name = name, InitialBalance = initial, CurrencyCode = currencyCode ?? Base, CreatedDate = created ?? new DateTime(2026, 1, 1)
        };
        await _accounts.SaveAsync(account);
        return account;
    }

    private Task AddAsync(TransactionType type, decimal amount, DateTime date, int accountId) =>
        Db.InsertAsync(new Transaction { Type = type, Amount = amount, Date = date, AccountId = accountId, CategoryId = 1 });

    private Task TransferAsync(int from, int to, decimal amount, DateTime date) =>
        Db.InsertAsync(new Transfer { SourceAccountId = from, DestinationAccountId = to, Amount = amount, DestinationAmount = amount, Date = date });

    private static SavingsGoal Goal(string name = "Trip", decimal target = 3000m, DateTime? targetDate = null) =>
        new() { Name = name, TargetAmount = target, CurrencyCode = Base, TargetDate = targetDate };

    [Fact]
    public async Task Saved_is_the_balance_of_the_linked_accounts_and_the_pace_their_recent_change()
    {
        var checking = await AddAccountAsync("Checking", 5000m);
        var savings = await AddAccountAsync("Savings", 1000m);
        var box = await AddAccountAsync("Box", 200m);
        // Within the last 3 months: +900 into the goal's accounts (a move between them doesn't count)
        await TransferAsync(checking.Id, savings.Id, 600m, Today.AddMonths(-2));
        await AddAsync(TransactionType.Income, 300m, Today.AddDays(-10), box.Id);
        await TransferAsync(savings.Id, box.Id, 100m, Today.AddDays(-5));
        // Before the window: in the balance, not in the pace
        await AddAsync(TransactionType.Income, 400m, Today.AddMonths(-5), savings.Id);
        await _sut.SaveAsync(Goal(targetDate: Today.AddMonths(6)), [savings.Id, box.Id]);

        var progress = (await _sut.GetProgressAsync()).Should().ContainSingle().Subject;

        progress.Saved.Should().Be(1000m + 600m + 400m + 200m + 300m);
        progress.MonthlyPace.Should().BeApproximately(300m, 3m);
        progress.AccountIds.Should().BeEquivalentTo([savings.Id, box.Id]);
        progress.Status.Should().Be(GoalStatus.OnTrack, "500 left at 300 a month is reached in 2 months");
    }

    [Fact]
    public async Task A_new_account_has_no_pace_yet()
    {
        var savings = await AddAccountAsync("Savings", 1000m, created: Today.AddDays(-7));
        await _sut.SaveAsync(Goal(), [savings.Id]);

        var progress = (await _sut.GetProgressAsync()).Single();

        progress.MonthlyPace.Should().BeNull();
        progress.Status.Should().Be(GoalStatus.None);
    }

    [Fact]
    public async Task The_pace_counts_entries_from_the_first_day_of_the_window()
    {
        var created = Today.AddDays(-60);
        var savings = await AddAccountAsync("Savings", created: created);
        // A statement import or recurring row on the creation day is dated 00:00
        await AddAsync(TransactionType.Income, 600m, created, savings.Id);
        await _sut.SaveAsync(Goal(), [savings.Id]);

        var progress = (await _sut.GetProgressAsync()).Single();

        progress.MonthlyPace.Should().BeApproximately(300m, 10m, "600 over about two months");
    }

    [Fact]
    public async Task Balances_the_caller_already_has_are_used()
    {
        var savings = await AddAccountAsync("Savings", 1000m);
        await _sut.SaveAsync(Goal(), [savings.Id]);

        var progress = (await _sut.GetProgressAsync([new AccountBalanceInfo { AccountId = savings.Id, CurrentBalance = 1234m }])).Single();

        progress.Saved.Should().Be(1234m);
    }

    [Fact]
    public async Task An_account_that_changed_currency_is_listed_but_not_counted()
    {
        var savings = await AddAccountAsync("Savings", 1000m);
        var other = await AddAccountAsync("Other", 500m);
        await _sut.SaveAsync(Goal(), [savings.Id, other.Id]);
        other.CurrencyCode = Base == "EUR" ? "USD" : "EUR";
        await _accounts.SaveAsync(other);

        var progress = (await _sut.GetProgressAsync()).Single();

        progress.Saved.Should().Be(1000m);
        progress.HasUncounted.Should().BeTrue();
        progress.AccountIds.Should().BeEquivalentTo([savings.Id, other.Id]);
        progress.UncountedAccountNames.Should().Be("Other");
    }

    [Fact]
    public async Task Goals_list_open_ones_by_date_and_reached_ones_last()
    {
        var a = await AddAccountAsync("A", 5000m);
        var b = await AddAccountAsync("B", 10m);
        var c = await AddAccountAsync("C", 10m);
        await _sut.SaveAsync(Goal("Done", 1000m), [a.Id]);
        await _sut.SaveAsync(Goal("No date"), [b.Id]);
        await _sut.SaveAsync(Goal("Soon", targetDate: Today.AddMonths(2)), [c.Id]);

        (await _sut.GetProgressAsync()).Select(p => p.Name).Should().Equal("Soon", "No date", "Done");
    }

    [Fact]
    public async Task Saving_replaces_the_accounts_and_refuses_another_goals_or_currencys_account()
    {
        var a = await AddAccountAsync("A");
        var b = await AddAccountAsync("B");
        var euros = await AddAccountAsync("Euros", currencyCode: Base == "EUR" ? "USD" : "EUR");
        var trip = Goal("Trip");
        await _sut.SaveAsync(trip, [a.Id]);
        await _sut.SaveAsync(trip, [b.Id]);

        (await _sut.GetLinkedAccountsAsync()).Should().BeEquivalentTo(new Dictionary<int, int> { [b.Id] = trip.Id });

        var car = Goal("Car");
        await FluentActions.Awaiting(() => _sut.SaveAsync(car, [b.Id])).Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Awaiting(() => _sut.SaveAsync(car, [euros.Id])).Should().ThrowAsync<InvalidOperationException>();
        (await Db.Table<SavingsGoal>().CountAsync()).Should().Be(1, "a refused goal is not added");
    }

    [Fact]
    public async Task Deleting_a_goal_or_an_account_removes_the_links_only()
    {
        var a = await AddAccountAsync("A");
        var b = await AddAccountAsync("B");
        var trip = Goal("Trip");
        await _sut.SaveAsync(trip, [a.Id, b.Id]);

        await new AccountService(_dbInitializer, _accounts).DeleteAsync(a.Id);
        (await _sut.GetLinkedAccountsAsync()).Keys.Should().Equal(b.Id);

        await _sut.DeleteAsync(trip.Id);
        (await Db.Table<SavingsGoalAccount>().CountAsync()).Should().Be(0);
        (await Db.Table<SavingsGoal>().CountAsync()).Should().Be(0);
        (await _accounts.GetByIdAsync(b.Id)).Should().NotBeNull("the account and its money stay");
    }

    // ---- Pockets ----

    private async Task<Pocket> AddPocketAsync(Account account, decimal held)
    {
        var pocket = new Pocket { AccountId = account.Id, Name = "Travel", CreatedDate = new DateTime(2026, 1, 1) };
        await Db.InsertAsync(pocket);
        if (held != 0)
            await Db.InsertAsync(new PocketMovement { PocketId = pocket.Id, Amount = held, Date = Today.AddDays(-3) });
        return pocket;
    }

    [Fact]
    public async Task A_linked_pocket_counts_its_money_not_its_accounts_and_adds_to_the_pace()
    {
        var bank = await AddAccountAsync("Bank", 2000m);
        var pocket = await AddPocketAsync(bank, 500m);
        await _sut.SaveAsync(Goal(), [], [pocket.Id]);

        var progress = (await _sut.GetProgressAsync()).Single();

        progress.Saved.Should().Be(500m);
        progress.PocketIds.Should().Equal(pocket.Id);
        progress.AccountNames.Should().Be("Bank › Travel");
        progress.MonthlyPace.Should().BeGreaterThan(0m, "500 was set aside inside the pace window");
    }

    [Fact]
    public async Task A_pocket_cannot_join_a_goal_with_its_own_account()
    {
        var bank = await AddAccountAsync("Bank", 2000m);
        var pocket = await AddPocketAsync(bank, 500m);

        var act = () => _sut.SaveAsync(Goal(), [bank.Id], [pocket.Id]);

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await Db.Table<SavingsGoal>().CountAsync()).Should().Be(0, "the goal rolls back");
    }

    [Fact]
    public async Task A_pocket_in_another_currency_or_in_another_goal_is_refused()
    {
        var bank = await AddAccountAsync("Bank", 2000m);
        var foreign = await AddAccountAsync("Foreign", 100m, Base == "EUR" ? "USD" : "EUR");
        var pocket = await AddPocketAsync(bank, 500m);
        var foreignPocket = await AddPocketAsync(foreign, 10m);
        await _sut.SaveAsync(Goal("First"), [], [pocket.Id]);

        var otherCurrency = () => _sut.SaveAsync(Goal("Second"), [], [foreignPocket.Id]);
        var taken = () => _sut.SaveAsync(Goal("Third"), [], [pocket.Id]);

        await otherCurrency.Should().ThrowAsync<InvalidOperationException>();
        await taken.Should().ThrowAsync<InvalidOperationException>();
        (await _sut.GetLinkedPocketsAsync()).Should().ContainSingle().Which.Key.Should().Be(pocket.Id);
    }

    [Fact]
    public async Task Saving_without_pocket_ids_keeps_the_goals_pockets_and_deleting_the_goal_removes_the_links()
    {
        var bank = await AddAccountAsync("Bank", 2000m);
        var pocket = await AddPocketAsync(bank, 500m);
        var goal = Goal();
        await _sut.SaveAsync(goal, [], [pocket.Id]);

        goal.Name = "Renamed";
        await _sut.SaveAsync(goal, []);
        (await _sut.GetLinkedPocketsAsync()).Should().ContainKey(pocket.Id);

        await _sut.DeleteAsync(goal.Id);
        (await _sut.GetLinkedPocketsAsync()).Should().BeEmpty();
        (await Db.FindAsync<Pocket>(pocket.Id)).Should().NotBeNull("the pocket stays");
    }
}
