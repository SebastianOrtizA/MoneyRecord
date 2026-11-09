using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Repositories;
using MoneyRecord.Services.Repositories.Migrations;
using SQLite;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>TagService and the tag table against a real temp SQLite file.</summary>
public sealed class TagServiceTests : IAsyncLifetime
{
    private readonly string _appData = Path.Combine(Path.GetTempPath(), $"moneyrecord-tags-{Guid.NewGuid():N}");
    private DatabaseInitializer _dbInitializer = null!;
    private TransactionRepository _transactions = null!;
    private TagService _sut = null!;

    private SQLiteAsyncConnection Db { get; set; } = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_appData);
        _dbInitializer = new DatabaseInitializer(new AppPaths(_appData));
        Db = await _dbInitializer.GetConnectionAsync();
        _transactions = new TransactionRepository(_dbInitializer);
        _sut = new TagService(_dbInitializer);
    }

    public async Task DisposeAsync()
    {
        await Db.CloseAsync();
        Directory.Delete(_appData, recursive: true);
    }

    private async Task<Transaction> AddAsync(string? tags, DateTime? date = null)
    {
        var transaction = new Transaction { Tags = tags, Date = date ?? new DateTime(2026, 5, 1), CategoryId = 1, Type = TransactionType.Expense, Amount = 5m };
        await _transactions.SaveAsync(transaction);
        return transaction;
    }

    private async Task<List<string>> RowsOfAsync(int transactionId) =>
        (await Db.Table<TransactionTag>().Where(t => t.TransactionId == transactionId).ToListAsync()).Select(t => t.Tag).Order().ToList();

    [Fact]
    public async Task Saving_a_transaction_writes_its_tag_rows_and_saving_again_replaces_them()
    {
        var transaction = await AddAsync("trip, food");
        (await RowsOfAsync(transaction.Id)).Should().Equal("food", "trip");

        transaction.Tags = "food, gift";
        await _transactions.SaveAsync(transaction);
        (await RowsOfAsync(transaction.Id)).Should().Equal("food", "gift");

        transaction.Tags = null;
        await _transactions.SaveAsync(transaction);
        (await RowsOfAsync(transaction.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_counts_each_tag_once_per_transaction_ignoring_case_and_skips_deleted_transactions()
    {
        await AddAsync("Food, trip", new DateTime(2026, 5, 1));
        await AddAsync("food", new DateTime(2026, 6, 1));
        var gone = await AddAsync("trip");
        await _transactions.DeleteAsync(gone);

        var all = await _sut.GetAllAsync();

        all.Select(t => (t.Name, t.TransactionCount)).Should().Equal(("Food", 1 + 1), ("trip", 1));
        all[0].LastUsed.Should().Be(new DateTime(2026, 6, 1));
    }

    [Fact]
    public async Task Rename_changes_the_tag_on_every_transaction_and_keeps_the_others()
    {
        var one = await AddAsync("trip, food");
        var two = await AddAsync("Trip");
        var other = await AddAsync("gift");

        (await _sut.RenameAsync("TRIP", "holiday")).Should().Be(2);

        (await Db.GetAsync<Transaction>(one.Id)).Tags.Should().Be("holiday, food");
        (await Db.GetAsync<Transaction>(two.Id)).Tags.Should().Be("holiday");
        (await Db.GetAsync<Transaction>(other.Id)).Tags.Should().Be("gift");
        (await RowsOfAsync(one.Id)).Should().Equal("food", "holiday");
    }

    [Fact]
    public async Task Rename_onto_an_existing_tag_merges_without_a_duplicate()
    {
        var both = await AddAsync("trip, holiday");
        var onlyTrip = await AddAsync("trip");

        await _sut.RenameAsync("trip", "Holiday");

        (await Db.GetAsync<Transaction>(both.Id)).Tags.Should().Be("Holiday");
        (await Db.GetAsync<Transaction>(onlyTrip.Id)).Tags.Should().Be("Holiday");
        (await _sut.GetAllAsync()).Should().ContainSingle().Which.TransactionCount.Should().Be(2);
    }

    [Fact]
    public async Task Delete_removes_only_that_tag()
    {
        var both = await AddAsync("trip, food");
        var onlyTrip = await AddAsync("trip");

        (await _sut.DeleteAsync("trip")).Should().Be(2);

        (await Db.GetAsync<Transaction>(both.Id)).Tags.Should().Be("food");
        (await Db.GetAsync<Transaction>(onlyTrip.Id)).Tags.Should().BeNull();
        (await _sut.GetAllAsync()).Select(t => t.Name).Should().Equal("food");
    }

    [Fact]
    public async Task Renaming_to_an_empty_name_is_refused_and_an_unknown_tag_changes_nothing()
    {
        await AddAsync("trip");

        await FluentActions.Awaiting(() => _sut.RenameAsync("trip", " , ")).Should().ThrowAsync<InvalidOperationException>();
        (await _sut.RenameAsync("nothing", "x")).Should().Be(0);
    }

    [Fact]
    public async Task Migration_and_rebuild_fill_the_table_from_the_comma_separated_tags()
    {
        var transaction = new Transaction { Tags = "a, b", CategoryId = 1, Date = DateTime.Today, Amount = 1m };
        await Db.InsertAsync(transaction); // bypasses the repository, like a row from before the table existed
        await Db.Table<TransactionTag>().DeleteAsync(_ => true);

        await Db.RunInTransactionAsync(connection => new M005_TransactionTags().Up(connection));
        (await RowsOfAsync(transaction.Id)).Should().Equal("a", "b");

        await Db.RunInTransactionAsync(TransactionTags.RebuildAll);
        (await Db.Table<TransactionTag>().CountAsync()).Should().Be(2);
    }
}
