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
        _sut = new StatementImportService(_database, categories,
            new PayeeService(_database, new AccountRepository(_database)));

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

    private static ParsedStatement Ofx(params (string Id, string Date, string Text, decimal Amount)[] rows) =>
        new(rows.Select((r, i) => new StatementEntry(i + 1, DateTime.Parse(r.Date), r.Text, r.Amount, r.Id)).ToList(), "EUR");

    [Fact]
    public async Task A_row_with_a_bank_id_is_a_duplicate_once_that_id_is_recorded()
    {
        var statement = Ofx(("A1", "2026-10-01", "Coffee", -3.5m), ("A2", "2026-10-01", "Coffee", -3.5m));
        var first = await _sut.PreviewAsync(statement, _bank, _food, _salary);
        first.Should().OnlyContain(r => !r.IsDuplicate);
        await _sut.ImportAsync(_bank, first);

        (await Db.Table<Transaction>().ToListAsync()).Select(t => t.ExternalId).Should().BeEquivalentTo(["A1", "A2"]);

        // The same file again; and a third equal coffee with a new ID is new, not matched to the imported ones
        var again = await _sut.PreviewAsync(Ofx(("A1", "2026-10-01", "Coffee", -3.5m), ("A2", "2026-10-01", "Coffee", -3.5m),
            ("A3", "2026-10-01", "Coffee", -3.5m)), _bank, _food, _salary);
        again.Select(r => r.IsDuplicate).Should().Equal(true, true, false);
    }

    [Fact]
    public async Task A_row_with_a_bank_id_also_matches_an_entry_typed_in_by_hand()
    {
        await Db.InsertAsync(new Transaction { Date = new(2026, 10, 1), Description = "Coffee", CategoryId = _food.Id,
            Type = TransactionType.Expense, AccountId = _bank.Id, AmountCents = 350 });

        var rows = await _sut.PreviewAsync(Ofx(("B1", "2026-10-01", "coffee", -3.5m), ("B2", "2026-10-01", "coffee", -3.5m)),
            _bank, _food, _salary);

        rows.Select(r => r.IsDuplicate).Should().Equal(true, false);
    }

    [Fact]
    public async Task A_file_in_another_currency_than_the_account_is_all_problems()
    {
        var statement = Ofx(("C1", "2026-10-01", "Coffee", -3.5m)) with { CurrencyCode = "USD" };

        var rows = await _sut.PreviewAsync(statement, _bank, _food, _salary);

        rows.Single().Problem.Should().Be(ImportProblem.Currency);
        rows.Single().IsIncluded.Should().BeFalse();
    }

    [Fact]
    public async Task A_reused_bank_id_with_another_amount_is_a_new_row_and_the_same_id_twice_in_a_file_counts_once()
    {
        await _sut.ImportAsync(_bank, await _sut.PreviewAsync(Ofx(("1", "2026-09-01", "Rent", -500m)), _bank, _food, _salary));

        var rows = await _sut.PreviewAsync(Ofx(("1", "2026-10-01", "Gym", -30m), ("1", "2026-10-01", "Gym", -30m),
            ("1", "2026-09-01", "Rent", -500m)), _bank, _food, _salary);

        // Same ID as the rent but another amount: new; its twin in the file: duplicate; the rent itself: recorded
        rows.Select(r => r.IsDuplicate).Should().Equal(false, true, true);
    }

    private const string Ynab =
        "\"Account\",\"Flag\",\"Date\",\"Payee\",\"Category Group/Category\",\"Category Group\",\"Category\",\"Memo\",\"Outflow\",\"Inflow\",\"Cleared\"\n" +
        "\"Checking\",\"\",\"10/05/2026\",\"Corner Cafe\",\"Food: Food test\",\"Food\",\"Food test\",\"\",\"$3.50\",\"$0.00\",\"Cleared\"\n" +
        "\"Checking\",\"\",\"10/06/2026\",\"Acme Inc\",\"Income: Salary test\",\"Income\",\"Salary test\",\"\",\"$0.00\",\"$1,500.00\",\"Cleared\"\n" +
        "\"Checking\",\"\",\"10/07/2026\",\"Gym\",\"Health: Fitness\",\"Health\",\"Fitness\",\"\",\"$30.00\",\"$0.00\",\"Cleared\"\n";

    private Task<List<ImportRow>> PreviewPresetAsync(string csv, bool createMissing = false)
    {
        var table = CsvTable.Parse(csv);
        var preset = StatementPresets.Detect(table, StatementMapping.Guess(table)!);
        return _sut.PreviewAsync(table, preset!.Mapping, _bank, _food, _salary, createMissing);
    }

    [Fact]
    public async Task A_YNAB_export_reads_outflow_and_inflow_categories_and_payees()
    {
        var rows = await PreviewPresetAsync(Ynab);

        rows.Select(r => (r.Type, r.AmountCents)).Should().Equal(
            (TransactionType.Expense, 350L), (TransactionType.Income, 150000L), (TransactionType.Expense, 3000L));
        rows.Select(r => r.Date).Should().Equal(new DateTime(2026, 10, 5), new DateTime(2026, 10, 6), new DateTime(2026, 10, 7));
        rows[0].Category!.Id.Should().Be(_food.Id);
        rows[1].Category!.Id.Should().Be(_salary.Id);
        rows.Select(r => r.PayeeName).Should().Equal("Corner Cafe", "Acme Inc", "Gym");
        // "Fitness" doesn't exist: the default category, not a guess
        rows[2].Category.Should().BeSameAs(_food);
    }

    [Fact]
    public async Task Missing_categories_are_created_on_import_only_when_asked_and_payees_are_saved()
    {
        var rows = await PreviewPresetAsync(Ynab, createMissing: true);
        rows[2].Category!.Name.Should().Be("Fitness");
        rows[2].Category!.Id.Should().Be(0);

        await _sut.ImportAsync(_bank, rows);

        var fitness = (await Db.Table<Category>().ToListAsync()).Single(c => c.Name == "Fitness");
        var gym = (await Db.Table<Transaction>().ToListAsync()).Single(t => t.Description == "Gym");
        gym.CategoryId.Should().Be(fitness.Id);
        gym.PayeeId.Should().NotBeNull();
        (await Db.Table<Payee>().ToListAsync()).Select(p => p.Name).Should().BeEquivalentTo("Corner Cafe", "Acme Inc", "Gym");
    }

    [Fact]
    public async Task A_Mint_export_signs_the_amount_by_its_transaction_type()
    {
        var csv =
            "\"Date\",\"Description\",\"Original Description\",\"Amount\",\"Transaction Type\",\"Category\",\"Account Name\",\"Labels\",\"Notes\"\n" +
            "\"10/05/2026\",\"Cafe\",\"CAFE 123\",\"3.50\",\"debit\",\"food test\",\"Checking\",\"\",\"\"\n" +
            "\"10/06/2026\",\"Acme\",\"ACME PAYROLL\",\"1500.00\",\"credit\",\"Salary test\",\"Checking\",\"\",\"\"\n";

        var rows = await PreviewPresetAsync(csv);

        rows.Select(r => (r.Type, r.AmountCents, r.Category!.Id)).Should().Equal(
            (TransactionType.Expense, 350L, _food.Id), (TransactionType.Income, 150000L, _salary.Id));
    }

    [Fact]
    public void Other_files_are_not_presets()
    {
        var table = CsvTable.Parse("Date,Description,Amount\n2026-10-01,Coffee,-3.50\n2026-10-02,Salary,1500\n");

        StatementPresets.Detect(table, StatementMapping.Guess(table)!).Should().BeNull();
    }

    [Fact]
    public async Task A_YNAB_row_with_no_outflow_and_no_inflow_is_a_zero_problem_and_a_Mint_type_must_be_debit_or_credit()
    {
        var empty = Ynab.Replace("\"$3.50\",\"$0.00\"", "\"\",\"\"");
        (await PreviewPresetAsync(empty))[0].Problem.Should().Be(ImportProblem.ZeroAmount);

        var mint =
            "\"Date\",\"Description\",\"Original Description\",\"Amount\",\"Transaction Type\",\"Category\"\n" +
            "\"10/05/2026\",\"Visa\",\"VISA\",\"3.50\",\"credit card payment\",\"food test\"\n" +
            "\"10/06/2026\",\"Cafe\",\"CAFE\",\"2.00\",\"debit\",\"food test\"\n";
        (await PreviewPresetAsync(mint)).Select(r => r.Problem).Should().Equal(ImportProblem.Amount, ImportProblem.None);
    }
}
