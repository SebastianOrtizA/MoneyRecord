using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Repositories;
using SQLite;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>QuickEntryService against a real temp SQLite file.</summary>
public sealed class QuickEntryServiceTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 11, 2, 9, 30, 0);

    private readonly string _appData = Path.Combine(Path.GetTempPath(), $"moneyrecord-quick-{Guid.NewGuid():N}");
    private DatabaseInitializer _dbInitializer = null!;
    private AccountRepository _accounts = null!;
    private CategoryRepository _categories = null!;
    private QuickEntryService _sut = null!;
    private Category _food = null!;
    private Category _salary = null!;
    private Account _bank = null!;

    private SQLiteAsyncConnection Db { get; set; } = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_appData);
        _dbInitializer = new DatabaseInitializer(new AppPaths(_appData));
        Db = await _dbInitializer.GetConnectionAsync();
        _accounts = new AccountRepository(_dbInitializer);
        _categories = new CategoryRepository(_dbInitializer);
        var clock = new FakeTimeProvider(new DateTimeOffset(Now, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        _sut = new QuickEntryService(_dbInitializer, _accounts, _categories, clock);

        _food = new Category { Name = "Food", Type = CategoryType.Expense };
        _salary = new Category { Name = "Pay", Type = CategoryType.Income };
        await Db.InsertAsync(_food);
        await Db.InsertAsync(_salary);
        _bank = new Account { Name = "Bank", CreatedDate = new DateTime(2026, 1, 1) };
        await _accounts.SaveAsync(_bank);
    }

    public async Task DisposeAsync()
    {
        await Db.CloseAsync();
        Directory.Delete(_appData, recursive: true);
    }

    private QuickEntry Coffee(long cents = 350) => new()
    {
        Name = "Coffee", AmountCents = cents, CategoryId = _food.Id, AccountId = _bank.Id
    };

    private async Task<Transaction> AddCoffeeAsync(int daysAgo, int? payeeId = null, long cents = 350)
    {
        var transaction = new Transaction
        {
            Date = Now.AddDays(-daysAgo), Description = "Coffee", AmountCents = cents, CategoryId = _food.Id,
            AccountId = _bank.Id, PayeeId = payeeId, Type = TransactionType.Expense
        };
        await Db.InsertAsync(transaction);
        return transaction;
    }

    [Fact]
    public async Task Save_orders_new_entries_last_and_getall_fills_names()
    {
        await _sut.SaveAsync(Coffee());
        await _sut.SaveAsync(new QuickEntry { Name = "Pay", Type = TransactionType.Income, AmountCents = 0, CategoryId = _salary.Id, AccountId = _bank.Id });

        var all = await _sut.GetAllAsync();

        all.Select(i => i.Name).Should().Equal("Coffee", "Pay");
        all[0].CategoryName.Should().Be("Food");
        all[0].AccountName.Should().Be("Bank");
        all[1].AsksAmount.Should().BeTrue();
        all[1].IsBroken.Should().BeFalse();
    }

    [Fact]
    public async Task Save_refuses_a_nameless_entry_a_transfer_and_a_category_of_the_other_type()
    {
        var nameless = Coffee();
        nameless.Name = "  ";
        var transfer = Coffee();
        transfer.Type = TransactionType.Transfer;
        var wrongCategory = Coffee();
        wrongCategory.CategoryId = _salary.Id;

        foreach (var entry in new[] { nameless, transfer, wrongCategory })
        {
            var act = () => _sut.SaveAsync(entry);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        (await _sut.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Save_refuses_the_travel_category()
    {
        var travel = new Category { Name = "Travel", Type = CategoryType.Expense, IsTravel = true };
        await Db.InsertAsync(travel);
        var entry = Coffee();
        entry.CategoryId = travel.Id;

        var act = () => _sut.SaveAsync(entry);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Move_swaps_neighbours_and_ignores_the_ends()
    {
        var first = await _sut.SaveAsync(Coffee());
        var second = Coffee();
        second.Name = "Bus";
        var secondId = await _sut.SaveAsync(second);

        await _sut.MoveAsync(first, up: true);
        (await _sut.GetAllAsync()).Select(i => i.Name).Should().Equal("Coffee", "Bus");

        await _sut.MoveAsync(secondId, up: true);
        (await _sut.GetAllAsync()).Select(i => i.Name).Should().Equal("Bus", "Coffee");

        await _sut.MoveAsync(first, up: false);
        (await _sut.GetAllAsync()).Select(i => i.Name).Should().Equal("Bus", "Coffee");
    }

    [Fact]
    public async Task Use_saves_a_transaction_dated_now_and_counts_the_use()
    {
        var id = await _sut.SaveAsync(Coffee());

        var saved = await _sut.UseAsync(id, null);

        saved.Date.Should().Be(Now);
        saved.AmountCents.Should().Be(350);
        saved.Type.Should().Be(TransactionType.Expense);
        saved.Description.Should().Be("Food");
        (await Db.Table<Transaction>().CountAsync()).Should().Be(1);
        var entry = (await _sut.GetAsync(id))!;
        entry.UseCount.Should().Be(1);
        entry.LastUsedDate.Should().Be(Now);
    }

    [Fact]
    public async Task Use_with_an_amount_replaces_the_stored_one_and_an_asking_entry_needs_it()
    {
        var asking = await _sut.SaveAsync(Coffee(0));

        var missing = () => _sut.UseAsync(asking, null);
        await missing.Should().ThrowAsync<InvalidOperationException>();
        (await Db.Table<Transaction>().CountAsync()).Should().Be(0);

        var saved = await _sut.UseAsync(asking, 1_275);
        saved.AmountCents.Should().Be(1_275);
    }

    [Fact]
    public async Task Use_keeps_the_payee_and_description()
    {
        var payee = new Payee { Name = "Corner Bakery" };
        await Db.InsertAsync(payee);
        var entry = Coffee();
        entry.PayeeId = payee.Id;
        entry.Description = "Morning coffee";
        var id = await _sut.SaveAsync(entry);

        var saved = await _sut.UseAsync(id, null);

        saved.PayeeId.Should().Be(payee.Id);
        saved.Description.Should().Be("Morning coffee");
    }

    [Fact]
    public async Task Use_of_a_missing_entry_throws()
    {
        var act = () => _sut.UseAsync(99, null);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Delete_removes_the_entry_and_keeps_its_transactions()
    {
        var id = await _sut.SaveAsync(Coffee());
        await _sut.UseAsync(id, null);

        await _sut.DeleteAsync(id);

        (await _sut.GetAllAsync()).Should().BeEmpty();
        (await Db.Table<Transaction>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Draft_is_named_after_the_payee_then_the_description_then_the_category()
    {
        var payee = new Payee { Name = "Corner Bakery" };
        await Db.InsertAsync(payee);
        var withPayee = await AddCoffeeAsync(1, payee.Id);
        var withDescription = await AddCoffeeAsync(1);
        var plain = await AddCoffeeAsync(1);
        plain.Description = "Food";
        await Db.UpdateAsync(plain);

        (await _sut.DraftFromTransactionAsync(withPayee.Id))!.Name.Should().Be("Corner Bakery");
        (await _sut.DraftFromTransactionAsync(withDescription.Id))!.Name.Should().Be("Coffee");
        var draft = (await _sut.DraftFromTransactionAsync(plain.Id))!;
        draft.Name.Should().Be("Food");
        draft.Description.Should().BeEmpty();
        draft.AmountCents.Should().Be(350);
        draft.AccountId.Should().Be(_bank.Id);
    }

    [Fact]
    public async Task Draft_is_refused_for_a_transfer_a_trip_expense_and_a_missing_transaction()
    {
        var trip = await AddCoffeeAsync(1);
        trip.TripId = 1;
        await Db.UpdateAsync(trip);
        var transfer = await AddCoffeeAsync(1);
        transfer.Type = TransactionType.Transfer;
        await Db.UpdateAsync(transfer);

        (await _sut.DraftFromTransactionAsync(trip.Id)).Should().BeNull();
        (await _sut.DraftFromTransactionAsync(transfer.Id)).Should().BeNull();
        (await _sut.DraftFromTransactionAsync(999)).Should().BeNull();
    }

    [Fact]
    public async Task Suggest_needs_three_equal_entries_in_the_last_60_days()
    {
        await AddCoffeeAsync(70);
        await AddCoffeeAsync(10, cents: 400);
        await AddCoffeeAsync(5);
        var latest = await AddCoffeeAsync(0);

        // Only two equal ones inside the window
        (await _sut.SuggestAsync(latest.Id)).Should().BeNull();

        await AddCoffeeAsync(20);
        (await _sut.SuggestAsync(latest.Id)).Should().NotBeNull().And.Match<QuickEntry>(d => d.AmountCents == 350);
    }

    [Fact]
    public async Task Suggest_separates_payees_and_skips_what_a_quick_entry_covers()
    {
        var payee = new Payee { Name = "Bakery" };
        await Db.InsertAsync(payee);
        await AddCoffeeAsync(3, payee.Id);
        await AddCoffeeAsync(2, payee.Id);
        var other = await AddCoffeeAsync(1);
        var latest = await AddCoffeeAsync(0, payee.Id);

        (await _sut.SuggestAsync(latest.Id)).Should().NotBeNull();
        (await _sut.SuggestAsync(other.Id)).Should().BeNull();

        await _sut.SaveAsync(Coffee());
        (await _sut.SuggestAsync(latest.Id)).Should().BeNull();
    }
}
