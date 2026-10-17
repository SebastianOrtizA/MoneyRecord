using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Repositories;
using SQLite;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>PayeeService against a real temp SQLite file (use counts are SQL, merges and deletes rewrite rows).</summary>
public sealed class PayeeServiceTests : IAsyncLifetime
{
    private readonly string _appData = Path.Combine(Path.GetTempPath(), $"moneyrecord-payees-{Guid.NewGuid():N}");
    private DatabaseInitializer _dbInitializer = null!;
    private PayeeService _sut = null!;

    private SQLiteAsyncConnection Db { get; set; } = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_appData);
        _dbInitializer = new DatabaseInitializer(new AppPaths(_appData));
        Db = await _dbInitializer.GetConnectionAsync();
        _sut = new PayeeService(_dbInitializer, new AccountRepository(_dbInitializer));
    }

    public async Task DisposeAsync()
    {
        await Db.CloseAsync();
        Directory.Delete(_appData, recursive: true);
    }

    private async Task<int> AddPayeeAsync(string name)
    {
        var payee = new Payee { Name = name };
        await Db.InsertAsync(payee);
        return payee.Id;
    }

    private async Task<Transaction> AddAsync(int? payeeId, DateTime date, int categoryId = 1, TransactionType type = TransactionType.Expense)
    {
        var transaction = new Transaction { PayeeId = payeeId, Date = date, CategoryId = categoryId, Type = type, Amount = 5m };
        await Db.InsertAsync(transaction);
        return transaction;
    }

    [Fact]
    public async Task Resolve_finds_a_payee_by_name_ignoring_case_and_spaces()
    {
        var bakery = await AddPayeeAsync("Corner Bakery");

        (await _sut.ResolveAsync("  corner   BAKERY ")).Should().Be(bakery);
        (await Db.Table<Payee>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Resolve_creates_a_new_payee_with_the_cleaned_name()
    {
        var id = await _sut.ResolveAsync("  Pharmacy   Plus ");

        (await Db.GetAsync<Payee>(id!.Value)).Name.Should().Be("Pharmacy Plus");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Resolve_returns_no_payee_for_an_empty_name(string? name)
    {
        (await _sut.ResolveAsync(name)).Should().BeNull();
        (await Db.Table<Payee>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GetAll_counts_transactions_their_latest_date_and_recurring_ones_by_name()
    {
        var shop = await AddPayeeAsync("shop");
        var bakery = await AddPayeeAsync("Bakery");
        var unused = await AddPayeeAsync("Unused");
        var scheduled = await AddPayeeAsync("Gym");
        await AddAsync(shop, new DateTime(2026, 10, 1));
        await AddAsync(shop, new DateTime(2026, 10, 9));
        await AddAsync(bakery, new DateTime(2026, 9, 3));
        await AddAsync(null, new DateTime(2026, 10, 12));
        await Db.InsertAsync(new RecurringTransaction { Description = "Weekly", PayeeId = shop });
        await Db.InsertAsync(new RecurringTransaction { Description = "Gym", PayeeId = scheduled });

        (await _sut.GetAllAsync()).Should().Equal(
            new PayeeSummary(bakery, "Bakery", 1, new DateTime(2026, 9, 3)),
            new PayeeSummary(scheduled, "Gym", 0, null, RecurringCount: 1),
            new PayeeSummary(shop, "shop", 2, new DateTime(2026, 10, 9), RecurringCount: 1),
            new PayeeSummary(unused, "Unused", 0, null));
    }

    [Fact]
    public async Task Last_category_is_the_latest_transaction_of_that_type()
    {
        var shop = await AddPayeeAsync("Shop");
        await AddAsync(shop, new DateTime(2026, 10, 1), categoryId: 3);
        await AddAsync(shop, new DateTime(2026, 10, 5), categoryId: 4);
        await AddAsync(shop, new DateTime(2026, 10, 9), categoryId: 9, type: TransactionType.Income);
        await AddAsync(null, new DateTime(2026, 10, 10), categoryId: 6);

        (await _sut.GetLastCategoryAsync(shop, TransactionType.Expense)).Should().Be(4);
        (await _sut.GetLastCategoryAsync(shop, TransactionType.Income)).Should().Be(9);
        (await _sut.GetLastCategoryAsync(await AddPayeeAsync("New"), TransactionType.Expense)).Should().BeNull();
    }

    [Fact]
    public async Task Rename_cleans_the_name_and_refuses_another_payee_s_name()
    {
        var shop = await AddPayeeAsync("Shop");
        await AddPayeeAsync("Bakery");

        await _sut.RenameAsync(shop, "  corner   shop ");
        (await Db.GetAsync<Payee>(shop)).Name.Should().Be("corner shop");

        await _sut.RenameAsync(shop, "Corner Shop");
        (await Db.GetAsync<Payee>(shop)).Name.Should().Be("Corner Shop", "a change of case is a rename, not a clash");

        await FluentActions.Awaiting(() => _sut.RenameAsync(shop, "BAKERY")).Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Awaiting(() => _sut.RenameAsync(shop, " ")).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Merge_moves_transactions_and_recurring_ones_then_deletes_the_payee()
    {
        var typo = await AddPayeeAsync("Bakry");
        var bakery = await AddPayeeAsync("Bakery");
        var other = await AddPayeeAsync("Other");
        await AddAsync(typo, new DateTime(2026, 10, 1));
        await AddAsync(bakery, new DateTime(2026, 10, 2));
        await AddAsync(other, new DateTime(2026, 10, 3));
        await Db.InsertAsync(new RecurringTransaction { Description = "Bread", PayeeId = typo });

        await _sut.MergeAsync(typo, bakery);

        (await Db.Table<Transaction>().OrderBy(t => t.Date).ToListAsync()).Select(t => t.PayeeId).Should().Equal(bakery, bakery, other);
        (await Db.Table<RecurringTransaction>().FirstAsync()).PayeeId.Should().Be(bakery);
        (await Db.FindAsync<Payee>(typo)).Should().BeNull();
    }

    [Fact]
    public async Task Merge_refuses_itself_and_a_missing_target_changing_nothing()
    {
        var shop = await AddPayeeAsync("Shop");
        await AddAsync(shop, new DateTime(2026, 10, 1));

        await FluentActions.Awaiting(() => _sut.MergeAsync(shop, shop)).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => _sut.MergeAsync(shop, 999)).Should().ThrowAsync<InvalidOperationException>();

        (await Db.Table<Transaction>().FirstAsync()).PayeeId.Should().Be(shop);
        (await Db.FindAsync<Payee>(shop)).Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_keeps_the_transactions_without_a_payee()
    {
        var shop = await AddPayeeAsync("Shop");
        var bakery = await AddPayeeAsync("Bakery");
        await AddAsync(shop, new DateTime(2026, 10, 1));
        await AddAsync(bakery, new DateTime(2026, 10, 2));
        await Db.InsertAsync(new RecurringTransaction { Description = "Weekly", PayeeId = shop });

        await _sut.DeleteAsync(shop);

        (await Db.Table<Transaction>().OrderBy(t => t.Date).ToListAsync()).Select(t => t.PayeeId).Should().Equal(null, bakery);
        (await Db.Table<RecurringTransaction>().FirstAsync()).PayeeId.Should().BeNull();
        (await Db.Table<Payee>().ToListAsync()).Select(p => p.Name).Should().Equal("Bakery");
    }
}
