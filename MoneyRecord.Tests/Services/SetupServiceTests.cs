using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;
using SQLite;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>SetupService against a real temp SQLite file, seeded like a new install (Cash + the default categories).</summary>
public sealed class SetupServiceTests : IAsyncLifetime
{
    private readonly string _appData = Path.Combine(Path.GetTempPath(), $"moneyrecord-setup-{Guid.NewGuid():N}");
    private readonly FakePreferences _preferences = new();
    private DatabaseInitializer _dbInitializer = null!;
    private SetupService _sut = null!;

    private SQLiteAsyncConnection Db { get; set; } = null!;

    private static readonly DefaultCategory Food = DefaultCategory.All.First(c => c.NameKey == "DefaultCategoryFood");
    private static readonly DefaultCategory Salary = DefaultCategory.All.First(c => c.NameKey == "DefaultCategorySalary");

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_appData);
        var paths = new AppPaths(_appData);
        _dbInitializer = new DatabaseInitializer(paths);
        _sut = new SetupService(_preferences, paths, _dbInitializer,
            new AccountRepository(_dbInitializer), new CategoryRepository(_dbInitializer));
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (Db != null)
            await Db.CloseAsync();
        Directory.Delete(_appData, recursive: true);
    }

    private async Task OpenAsync() => Db = await _dbInitializer.GetConnectionAsync();

    [Fact]
    public async Task A_new_install_is_pending_until_the_setup_is_applied()
    {
        _sut.IsFirstRunPending().Should().BeTrue();

        // Closing the app halfway: the database now exists, but the setup is still to do
        await OpenAsync();
        _sut.IsFirstRunPending().Should().BeTrue();

        await _sut.ApplyAsync(new SetupChoices("USD", [new SetupAccount("Cash", 0m, Account.CashIconCode)], [], true));
        _sut.IsFirstRunPending().Should().BeFalse();
    }

    [Fact]
    public async Task An_existing_database_counts_as_set_up()
    {
        await OpenAsync();

        _sut.IsFirstRunPending().Should().BeFalse("an update from an older version already has its data");
    }

    [Fact]
    public async Task First_run_replaces_the_seeded_accounts_and_categories()
    {
        await OpenAsync();

        await _sut.ApplyAsync(new SetupChoices("EUR",
            [new SetupAccount("Wallet", 20.5m, Account.CashIconCode), new SetupAccount("Bank", 1500m, "F0070")],
            [Food, Salary],
            ReplaceStartingData: true));

        var accounts = await Db.Table<Account>().OrderBy(a => a.Id).ToListAsync();
        accounts.Select(a => (a.Name, a.InitialBalance, a.CurrencyCode, a.IsDefault))
            .Should().Equal(("Wallet", 20.5m, "EUR", true), ("Bank", 1500m, "EUR", false));
        accounts.Should().OnlyContain(a => a.AllowNegativeBalance);

        var categories = await Db.Table<Category>().ToListAsync();
        categories.Select(c => (c.Name, c.Type)).Should().BeEquivalentTo([(Food.Name, Food.Type), (Salary.Name, Salary.Type)]);
    }

    [Fact]
    public async Task First_run_without_accounts_is_refused_and_changes_nothing()
    {
        _sut.IsFirstRunPending().Should().BeTrue();
        await OpenAsync();

        var act = () => _sut.ApplyAsync(new SetupChoices("USD", [], [Food], ReplaceStartingData: true));

        await act.Should().ThrowAsync<ArgumentException>();
        (await Db.Table<Account>().CountAsync()).Should().Be(1);
        (await Db.Table<Category>().CountAsync()).Should().Be(DefaultCategory.All.Count);
        _sut.IsFirstRunPending().Should().BeTrue();
    }

    [Fact]
    public async Task Recorded_entries_keep_their_accounts_and_categories()
    {
        await OpenAsync();
        var cash = await Db.Table<Account>().FirstAsync();
        await Db.InsertAsync(new Transaction { Description = "Lunch", Amount = 10m, AccountId = cash.Id });

        await _sut.ApplyAsync(new SetupChoices("USD", [new SetupAccount("Bank", 0m, "F0070")], [Food],
            ReplaceStartingData: true));

        (await Db.Table<Account>().CountAsync()).Should().Be(2, "Cash stays and Bank is added");
        (await Db.Table<Category>().CountAsync()).Should().Be(DefaultCategory.All.Count, "Food already exists");
        (await Db.Table<Account>().CountAsync(a => a.IsDefault)).Should().Be(1);
    }

    [Fact]
    public async Task From_settings_only_adds_what_is_missing()
    {
        await OpenAsync();
        var food = await Db.Table<Category>().FirstAsync(c => c.Name == Food.Name);
        await Db.DeleteAsync(food);

        await _sut.ApplyAsync(new SetupChoices("USD", [new SetupAccount("Bank", 0m, "F0070")], [Food, Salary],
            ReplaceStartingData: false));

        (await Db.Table<Category>().CountAsync()).Should().Be(DefaultCategory.All.Count, "Food comes back, Salary exists");
        var bank = await Db.Table<Account>().FirstAsync(a => a.Name == "Bank");
        bank.IsDefault.Should().BeFalse("Cash is still the default");
    }
}
