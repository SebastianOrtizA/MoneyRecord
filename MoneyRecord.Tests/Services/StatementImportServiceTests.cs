using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Repositories;
using SQLite;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>StatementImportService against a real temp SQLite file (seeded like a new install).</summary>
public sealed class StatementImportServiceTests : IAsyncLifetime
{
    private readonly string _appData = Path.Combine(Path.GetTempPath(), $"moneyrecord-import-{Guid.NewGuid():N}");
    private DatabaseInitializer _database = null!;
    private StatementImportService _sut = null!;
    private Account _bank = null!;
    private Category _food = null!, _transport = null!, _salary = null!;

    private SQLiteAsyncConnection Db { get; set; } = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_appData);
        _database = new DatabaseInitializer(new AppPaths(_appData));
        Db = await _database.GetConnectionAsync();
        var categories = new CategoryRepository(_database);
        _sut = new StatementImportService(_database, categories);

        _bank = new Account { Name = "Bank", CurrencyCode = "EUR" };
        await new AccountRepository(_database).SaveAsync(_bank);
        _food = await AddCategoryAsync("Food test", CategoryType.Expense);
        _transport = await AddCategoryAsync("Transport test", CategoryType.Expense);
        _salary = await AddCategoryAsync("Salary test", CategoryType.Income);
    }

    public async Task DisposeAsync()
    {
        await Db.CloseAsync();
        Directory.Delete(_appData, recursive: true);
    }

    private async Task<Category> AddCategoryAsync(string name, CategoryType type)
    {
        var category = new Category { Name = name, Type = type };
        await Db.InsertAsync(category);
        return category;
    }

    private Task<List<ImportRow>> PreviewAsync(string csv, bool positiveIsExpense = false)
    {
        var table = CsvTable.Parse(csv);
        var mapping = StatementMapping.Guess(table)!.Mapping with { PositiveIsExpense = positiveIsExpense };
        return _sut.PreviewAsync(table, mapping, _bank, _food, _salary);
    }

    [Fact]
    public async Task Rows_become_expenses_and_income_by_sign()
    {
        var rows = await PreviewAsync("Date,Description,Amount\n2026-10-01,Coffee,-3.50\n2026-10-02,Salary,1500\n");

        rows.Select(r => (r.Type, r.AmountCents, r.Category!.Id)).Should().Equal(
            (TransactionType.Expense, 350L, _food.Id), (TransactionType.Income, 150_000L, _salary.Id));
        rows.Should().OnlyContain(r => r.IsIncluded && !r.IsDuplicate && r.CurrencyCode == "EUR");
    }

    [Fact]
    public async Task Positive_amounts_can_be_the_expenses()
    {
        var rows = await PreviewAsync("Date,Description,Amount\n2026-10-01,Taxi,25\n2026-10-02,Payment,-100\n",
            positiveIsExpense: true);

        rows.Select(r => r.Type).Should().Equal(TransactionType.Expense, TransactionType.Income);
    }

    [Fact]
    public async Task A_known_description_gets_its_latest_category_digits_ignored()
    {
        await Db.InsertAsync(new Transaction { Date = new(2026, 9, 1), Description = "UBER TRIP 1111", CategoryId = _food.Id,
            Type = TransactionType.Expense, Amount = 9m });
        await Db.InsertAsync(new Transaction { Date = new(2026, 9, 20), Description = "Uber trip 2222", CategoryId = _transport.Id,
            Type = TransactionType.Expense, Amount = 12m });

        var rows = await PreviewAsync("Date,Description,Amount\n2026-10-01,UBER TRIP 3333,-15\n2026-10-02,Bakery,-4\n");

        rows[0].Category!.Id.Should().Be(_transport.Id);
        rows[0].IsLearned.Should().BeTrue();
        rows[1].Category!.Id.Should().Be(_food.Id, "the default for new expenses");
        rows[1].IsLearned.Should().BeFalse();
    }

    [Fact]
    public async Task Recorded_entries_are_duplicates_counted_one_for_one()
    {
        await Db.InsertAsync(new Transaction { Date = new(2026, 10, 1, 9, 30, 0), Description = "coffee  shop", Amount = 3.5m,
            Type = TransactionType.Expense, CategoryId = _food.Id, AccountId = _bank.Id });
        // Same entry in another account: not a duplicate here
        await Db.InsertAsync(new Transaction { Date = new(2026, 10, 2), Description = "Bakery", Amount = 4m,
            Type = TransactionType.Expense, CategoryId = _food.Id, AccountId = _bank.Id + 100 });

        var rows = await PreviewAsync(
            "Date,Description,Amount\n2026-10-01,Coffee Shop,-3.50\n2026-10-01,Coffee Shop,-3.50\n2026-10-02,Bakery,-4\n");

        rows.Select(r => r.IsDuplicate).Should().Equal(true, false, false);
        rows.Select(r => r.IsIncluded).Should().Equal(false, true, true);
        rows[0].StatusText.Should().NotBeEmpty();
    }

    [Fact]
    public async Task A_split_payment_is_one_recorded_entry_with_its_total()
    {
        // An imported "Supermarket" row of 50.00, later split into food and household
        foreach (var (category, amount) in new[] { (_food.Id, 30m), (_transport.Id, 20m) })
            await Db.InsertAsync(new Transaction { Date = new(2026, 10, 1), Description = "Supermarket", Amount = amount,
                Type = TransactionType.Expense, CategoryId = category, AccountId = _bank.Id, SplitId = "s1" });

        var rows = await PreviewAsync("Date,Description,Amount\n2026-10-01,Supermarket,-50\n2026-10-01,Supermarket,-30\n");

        rows.Select(r => r.IsDuplicate).Should().Equal(true, false);
    }

    [Fact]
    public async Task Unreadable_rows_are_excluded_with_their_reason()
    {
        var rows = await PreviewAsync(
            "Date,Description,Amount\n2026-10-01,Coffee,-3.50\nTotal,,-3.50\n2026-10-03,Fee,abc\n2026-10-04,Nothing,0.00\n");

        rows.Select(r => r.Problem).Should().Equal(
            ImportProblem.None, ImportProblem.Date, ImportProblem.Amount, ImportProblem.ZeroAmount);
        rows.Skip(1).Should().OnlyContain(r => !r.IsIncluded && !r.CanImport && r.HasStatus);
    }

    [Fact]
    public async Task Import_adds_the_included_rows_and_undo_removes_them()
    {
        var rows = await PreviewAsync("Date,Description,Amount\n2026-10-01,Coffee,-3.50\n2026-10-02,Taxi,-20\n2026-10-03,Salary,1500\n");
        rows[1].IsIncluded = false;
        rows[2].Category = _salary;

        var ids = await _sut.ImportAsync(_bank, rows);

        var added = await Db.Table<Transaction>().Where(t => t.AccountId == _bank.Id).OrderBy(t => t.Date).ToListAsync();
        added.Select(t => t.Id).Should().BeEquivalentTo(ids);
        added.Select(t => (t.Date, t.Description, t.AmountCents, t.Type, t.CategoryId)).Should().Equal(
            (new DateTime(2026, 10, 1), "Coffee", 350L, TransactionType.Expense, _food.Id),
            (new DateTime(2026, 10, 3), "Salary", 150_000L, TransactionType.Income, _salary.Id));

        // A second import of the same file finds them recorded
        (await PreviewAsync("Date,Description,Amount\n2026-10-01,Coffee,-3.50\n")).Single().IsDuplicate.Should().BeTrue();

        await _sut.UndoAsync(ids);

        (await Db.Table<Transaction>().CountAsync(t => t.AccountId == _bank.Id)).Should().Be(0);
    }
}
