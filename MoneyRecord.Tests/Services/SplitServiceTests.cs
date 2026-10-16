using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Repositories;
using SQLite;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>SplitService against a real temp SQLite file: a split payment is plain transactions sharing a split ID.</summary>
public sealed class SplitServiceTests : IAsyncLifetime
{
    private readonly string _appData = Path.Combine(Path.GetTempPath(), $"moneyrecord-splits-{Guid.NewGuid():N}");
    private DatabaseInitializer _dbInitializer = null!;
    private TransactionRepository _transactions = null!;
    private SplitService _sut = null!;

    private SQLiteAsyncConnection Db { get; set; } = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_appData);
        _dbInitializer = new DatabaseInitializer(new AppPaths(_appData));
        Db = await _dbInitializer.GetConnectionAsync();
        _transactions = new TransactionRepository(_dbInitializer);
        _sut = new SplitService(_dbInitializer);
    }

    public async Task DisposeAsync()
    {
        await Db.CloseAsync();
        Directory.Delete(_appData, recursive: true);
    }

    private static Transaction Template(string? splitId = null) => new()
    {
        Date = new DateTime(2026, 10, 16), Type = TransactionType.Expense, AccountId = 2, PayeeId = 5, Tags = "home",
        ReceiptPhotoPath = "receipt.jpg", SplitId = splitId
    };

    private Task<List<Transaction>> AllAsync() => Db.Table<Transaction>().OrderBy(t => t.Id).ToListAsync();

    [Fact]
    public async Task Parts_become_transactions_sharing_the_payment_and_a_new_split_id()
    {
        var saved = await _sut.SaveAsync(Template(), [new(3, 3_000, "Food"), new(4, 2_000, "Cleaning")], []);

        var rows = await AllAsync();
        rows.Select(t => (t.CategoryId, t.AmountCents, t.Description)).Should().Equal((3, 3_000L, "Food"), (4, 2_000L, "Cleaning"));
        rows.Should().OnlyContain(t => t.Date == new DateTime(2026, 10, 16) && t.Type == TransactionType.Expense && t.AccountId == 2
            && t.PayeeId == 5 && t.Tags == "home" && t.ReceiptPhotoPath == "receipt.jpg");
        rows.Select(t => t.SplitId).Distinct().Should().ContainSingle().Which.Should().NotBeNullOrEmpty();
        saved.Select(t => t.Id).Should().Equal(rows.Select(t => t.Id));
        (await _transactions.GetSplitPartsAsync(rows[0].SplitId!)).Should().HaveCount(2);
    }

    [Fact]
    public async Task Editing_replaces_the_old_rows_and_keeps_the_split_id()
    {
        var first = await _sut.SaveAsync(Template(), [new(3, 3_000, "Food"), new(4, 2_000, "Cleaning")], []);
        var other = new Transaction { Description = "Other", Amount = 1m, Date = new DateTime(2026, 10, 1) };
        await Db.InsertAsync(other);

        await _sut.SaveAsync(Template(first[0].SplitId), [new(3, 2_500, "Food"), new(4, 1_500, "Cleaning"), new(6, 1_000, "Gift")],
            first.Select(t => t.Id).ToList());

        var rows = await AllAsync();
        rows.Select(t => t.Description).Should().Equal("Other", "Food", "Cleaning", "Gift");
        rows.Skip(1).Should().OnlyContain(t => t.SplitId == first[0].SplitId);
    }

    [Fact]
    public async Task One_part_is_a_plain_transaction_turning_a_split_back_or_a_plain_one_into_a_split()
    {
        var split = await _sut.SaveAsync(Template(), [new(3, 3_000, "Food"), new(4, 2_000, "Cleaning")], []);

        var single = await _sut.SaveAsync(Template(split[0].SplitId), [new(3, 5_000, "Food")], split.Select(t => t.Id).ToList());

        (await AllAsync()).Should().ContainSingle().Which.Should().BeEquivalentTo(new { AmountCents = 5_000L, SplitId = (string?)null });

        await _sut.SaveAsync(Template(), [new(3, 4_000, "Food"), new(4, 1_000, "Cleaning")], [single[0].Id]);

        var rows = await AllAsync();
        rows.Should().HaveCount(2).And.OnlyContain(t => t.SplitId != null && t.SplitId == rows[0].SplitId);
    }

    [Fact]
    public async Task The_own_conversion_is_shared_out_over_the_parts()
    {
        var template = Template();
        template.ConvertedAmountCents = 1_000;
        template.ConvertedCurrencyCode = "USD";

        await _sut.SaveAsync(template, [new(3, 3_000, "Food"), new(4, 2_000, "Cleaning")], []);

        (await AllAsync()).Select(t => (t.ConvertedAmountCents, t.ConvertedCurrencyCode)).Should().Equal((600L, "USD"), (400L, "USD"));
    }

    [Fact]
    public async Task Parts_without_money_are_refused_and_nothing_changes()
    {
        var existing = new Transaction { Description = "Keep", Amount = 1m };
        await Db.InsertAsync(existing);

        await FluentActions.Awaiting(() => _sut.SaveAsync(Template(), [new(3, 3_000, "Food"), new(4, 0, "Cleaning")], [existing.Id]))
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => _sut.SaveAsync(Template(), [], [existing.Id])).Should().ThrowAsync<ArgumentException>();

        (await AllAsync()).Should().ContainSingle().Which.Description.Should().Be("Keep");
    }
}
