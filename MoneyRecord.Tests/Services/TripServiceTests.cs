using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;
using NSubstitute;
using SQLite;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>TripService against a real temp SQLite file, clock at 15 March 2026 10:00 (UTC).</summary>
[Collection(LanguageCollection.Name)]
public sealed class TripServiceTests : LanguageTestBase, IAsyncLifetime
{
    private readonly string _appData = Path.Combine(Path.GetTempPath(), $"moneyrecord-trips-{Guid.NewGuid():N}");
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.Zero));
    private readonly IExchangeRateService _rates = Substitute.For<IExchangeRateService>();
    private DatabaseInitializer _dbInitializer = null!;
    private AccountRepository _accounts = null!;
    private CategoryRepository _categories = null!;
    private TripService _sut = null!;
    private Account _bank = null!;
    private Category _food = null!;
    private Category _fun = null!;

    private SQLiteAsyncConnection Db { get; set; } = null!;

    private static string Base => MoneyFormatter.BaseCurrency;

    public async Task InitializeAsync()
    {
        _clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        Directory.CreateDirectory(_appData);
        _dbInitializer = new DatabaseInitializer(new AppPaths(_appData));
        Db = await _dbInitializer.GetConnectionAsync();
        _accounts = new AccountRepository(_dbInitializer);
        _categories = new CategoryRepository(_dbInitializer);
        _rates.GetConverterAsync().Returns(new CurrencyConverter([]));
        _sut = new TripService(_dbInitializer, _accounts, _categories, _rates, _clock);

        _bank = new Account { Name = "Bank", InitialBalance = 1000m, CurrencyCode = Base };
        await _accounts.SaveAsync(_bank);
        _food = new Category { Name = "Dining", Type = CategoryType.Expense };
        _fun = new Category { Name = "Fun", Type = CategoryType.Expense };
        await _categories.SaveAsync(_food);
        await _categories.SaveAsync(_fun);
    }

    public async Task DisposeAsync()
    {
        await Db.CloseAsync();
        Directory.Delete(_appData, recursive: true);
    }

    private async Task<int> StartTripAsync(string name = "Lisbon", decimal? budget = null)
    {
        var trip = new Trip { Name = name, StartDate = new DateTime(2026, 3, 10), CurrencyCode = Base, Budget = budget };
        return await _sut.SaveAsync(trip, new Dictionary<int, decimal>());
    }

    private async Task<Transaction> SpendAsync(int tripId, int tripCategoryId, long cents, int? accountId = null)
    {
        var travel = await _sut.GetTravelCategoryIdAsync();
        var expense = new Transaction
        {
            Date = new DateTime(2026, 3, 12), Type = TransactionType.Expense, AmountCents = cents, CategoryId = travel,
            AccountId = accountId ?? _bank.Id, TripId = tripId, TripCategoryId = tripCategoryId, Description = "x"
        };
        await Db.InsertAsync(expense);
        return expense;
    }

    private async Task<List<TripCategory>> TripCategoriesAsync() => await _sut.GetCategoriesAsync();

    [Fact]
    public async Task NewDatabase_HasDefaultTripCategories()
    {
        (await TripCategoriesAsync()).Should().HaveCount(DefaultTripCategory.All.Count);
    }

    [Fact]
    public async Task OnlyOneTripCanBeActive()
    {
        await StartTripAsync();

        var second = () => StartTripAsync("Madrid");

        await second.Should().ThrowAsync<InvalidOperationException>();
        (await _sut.GetActiveAsync())!.Name.Should().Be("Lisbon");
    }

    [Fact]
    public async Task TravelCategory_IsCreatedOnce()
    {
        var first = await _sut.GetTravelCategoryIdAsync();
        var second = await _sut.GetTravelCategoryIdAsync();

        second.Should().Be(first);
        (await Db.Table<Category>().CountAsync(c => c.IsTravel)).Should().Be(1);
    }

    [Fact]
    public async Task Summary_SumsPerTripCategoryAndAgainstLimits()
    {
        var categories = await TripCategoriesAsync();
        var (lodging, food) = (categories[0], categories[3]);
        var trip = new Trip { Name = "Lisbon", CurrencyCode = Base, Budget = 400m };
        var id = await _sut.SaveAsync(trip, new Dictionary<int, decimal> { [lodging.Id] = 200m });
        await SpendAsync(id, lodging.Id, 15000);
        await SpendAsync(id, lodging.Id, 10000);
        await SpendAsync(id, food.Id, 3000);

        var summary = await _sut.GetSummaryAsync(id);

        summary.Spent.Should().Be(280m);
        summary.Level.Should().Be(BudgetLevel.Warning);
        summary.Categories.Should().HaveCount(2);
        var lodgingRow = summary.Categories.Single(c => c.Category.Id == lodging.Id);
        lodgingRow.Spent.Should().Be(250m);
        lodgingRow.Limit.Should().Be(200m);
        lodgingRow.Level.Should().Be(BudgetLevel.Exceeded);
        summary.NotConverted.Should().BeEmpty();
    }

    [Fact]
    public async Task Summary_KeepsAmountsWithoutARateInTheirOwnCurrency()
    {
        var other = new Account { Name = "Dollars", CurrencyCode = Base == "USD" ? "GBP" : "USD" };
        await _accounts.SaveAsync(other);
        var id = await StartTripAsync();
        await SpendAsync(id, (await TripCategoriesAsync())[0].Id, 5000, other.Id);

        var summary = await _sut.GetSummaryAsync(id);

        summary.Spent.Should().Be(0);
        summary.NotConverted.Should().ContainSingle().Which.Amount.Should().Be(50m);
    }

    [Fact]
    public async Task End_MovesEachTripCategoryToItsRealCategory_AndRemembersIt()
    {
        var categories = await TripCategoriesAsync();
        var (lodging, food) = (categories[0], categories[3]);
        var id = await StartTripAsync();
        var a = await SpendAsync(id, lodging.Id, 10000);
        var b = await SpendAsync(id, food.Id, 2000);

        await _sut.EndAsync(id, new Dictionary<int, int> { [lodging.Id] = _fun.Id, [food.Id] = _food.Id });

        (await Db.FindAsync<Transaction>(a.Id))!.CategoryId.Should().Be(_fun.Id);
        (await Db.FindAsync<Transaction>(b.Id))!.CategoryId.Should().Be(_food.Id);
        (await Db.FindAsync<Transaction>(a.Id))!.TripId.Should().Be(id);
        (await _sut.GetActiveAsync()).Should().BeNull();
        (await Db.FindAsync<TripCategory>(lodging.Id))!.LastCategoryId.Should().Be(_fun.Id);
    }

    [Fact]
    public async Task End_WithAMissingMapping_ChangesNothing()
    {
        var lodging = (await TripCategoriesAsync())[0];
        var id = await StartTripAsync();
        var a = await SpendAsync(id, lodging.Id, 10000);
        var travel = a.CategoryId;

        var end = () => _sut.EndAsync(id, new Dictionary<int, int>());

        await end.Should().ThrowAsync<InvalidOperationException>();
        (await Db.FindAsync<Transaction>(a.Id))!.CategoryId.Should().Be(travel);
        (await _sut.GetActiveAsync()).Should().NotBeNull();
    }

    [Fact]
    public async Task Reopen_MovesExpensesBackToTravel()
    {
        var lodging = (await TripCategoriesAsync())[0];
        var id = await StartTripAsync();
        var a = await SpendAsync(id, lodging.Id, 10000);
        await _sut.EndAsync(id, new Dictionary<int, int> { [lodging.Id] = _fun.Id });

        await _sut.ReopenAsync(id);

        (await Db.FindAsync<Transaction>(a.Id))!.CategoryId.Should().Be(await _sut.GetTravelCategoryIdAsync());
        (await _sut.GetActiveAsync())!.Id.Should().Be(id);
    }

    [Fact]
    public async Task Reopen_IsRefusedWhileAnotherTripIsActive()
    {
        var first = await StartTripAsync();
        await _sut.EndAsync(first, new Dictionary<int, int>());
        await StartTripAsync("Madrid");

        var reopen = () => _sut.ReopenAsync(first);

        await reopen.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Delete_ActiveTripWithExpenses_IsRefused()
    {
        var id = await StartTripAsync();
        await SpendAsync(id, (await TripCategoriesAsync())[0].Id, 100);

        var delete = () => _sut.DeleteAsync(id);

        await delete.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Delete_EndedTrip_KeepsTheExpensesInTheLedger()
    {
        var lodging = (await TripCategoriesAsync())[0];
        var id = await StartTripAsync();
        var a = await SpendAsync(id, lodging.Id, 10000);
        await _sut.EndAsync(id, new Dictionary<int, int> { [lodging.Id] = _fun.Id });

        await _sut.DeleteAsync(id);

        var kept = (await Db.FindAsync<Transaction>(a.Id))!;
        kept.TripId.Should().BeNull();
        kept.CategoryId.Should().Be(_fun.Id);
        (await _sut.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteCategory_InUse_NeedsAReplacement_ThenMovesExpensesAndLimits()
    {
        var categories = await TripCategoriesAsync();
        var (lodging, food) = (categories[0], categories[3]);
        var trip = new Trip { Name = "Lisbon", CurrencyCode = Base };
        var id = await _sut.SaveAsync(trip, new Dictionary<int, decimal> { [lodging.Id] = 100m });
        var a = await SpendAsync(id, lodging.Id, 10000);

        var without = () => _sut.DeleteCategoryAsync(lodging.Id, null);
        await without.Should().ThrowAsync<InvalidOperationException>();

        await _sut.DeleteCategoryAsync(lodging.Id, food.Id);

        (await Db.FindAsync<Transaction>(a.Id))!.TripCategoryId.Should().Be(food.Id);
        (await _sut.GetLimitsAsync(id)).Should().ContainKey(food.Id).WhoseValue.Should().Be(100m);
        (await _sut.GetCategoriesAsync()).Should().NotContain(c => c.Id == lodging.Id);
    }
}
