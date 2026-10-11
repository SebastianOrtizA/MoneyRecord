using FluentAssertions;
using MoneyRecord.Models;
using MoneyRecord.Services.Repositories;
using MoneyRecord.Services.Repositories.Migrations;
using SQLite;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>
/// v1 → v2 against a real temp SQLite file whose tables look like the ones older app versions created
/// (money in REAL columns, user_version 0).
/// </summary>
public sealed class MoneyAsCentsMigrationTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"moneyrecord-test-{Guid.NewGuid():N}.db3");
    private SQLiteAsyncConnection _db = null!;

    public async Task InitializeAsync()
    {
        _db = new SQLiteAsyncConnection(_dbPath);
        await _db.ExecuteAsync("CREATE TABLE \"Account\" (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name VARCHAR, InitialBalance FLOAT)");
        await _db.ExecuteAsync("CREATE TABLE \"Transaction\" (Id INTEGER PRIMARY KEY AUTOINCREMENT, Description VARCHAR, Amount FLOAT, Type INTEGER, AccountId INTEGER)");
        await _db.ExecuteAsync("CREATE TABLE \"Transfer\" (Id INTEGER PRIMARY KEY AUTOINCREMENT, Amount FLOAT, SourceAccountId INTEGER, DestinationAccountId INTEGER)");
        await _db.ExecuteAsync("CREATE TABLE \"Budget\" (Id INTEGER PRIMARY KEY AUTOINCREMENT, CategoryId INTEGER, LimitAmount FLOAT)");
        await _db.ExecuteAsync("CREATE TABLE \"RecurringTransaction\" (Id INTEGER PRIMARY KEY AUTOINCREMENT, Amount FLOAT)");
    }

    public async Task DisposeAsync()
    {
        await _db.CloseAsync();
        foreach (var file in Directory.GetFiles(Path.GetDirectoryName(_dbPath)!, Path.GetFileName(_dbPath) + "*"))
            File.Delete(file);
    }

    [Fact]
    public async Task MovesEveryMoneyColumnToCents()
    {
        await _db.ExecuteAsync("INSERT INTO \"Account\" (Name, InitialBalance) VALUES ('Bank', 1250.5)");
        await _db.ExecuteAsync("INSERT INTO \"Transaction\" (Description, Amount) VALUES ('Lunch', 12.34)");
        await _db.ExecuteAsync("INSERT INTO \"Transfer\" (Amount) VALUES (99.99)");
        await _db.ExecuteAsync("INSERT INTO \"Budget\" (CategoryId, LimitAmount) VALUES (1, 300)");
        await _db.ExecuteAsync("INSERT INTO \"RecurringTransaction\" (Amount) VALUES (15.75)");

        await MigrateAsync();

        (await _db.Table<Account>().FirstAsync()).InitialBalanceCents.Should().Be(125050);
        (await _db.Table<Transaction>().FirstAsync()).AmountCents.Should().Be(1234);
        (await _db.Table<Transfer>().FirstAsync()).AmountCents.Should().Be(9999);
        (await _db.Table<Budget>().FirstAsync()).LimitAmountCents.Should().Be(30000);
        (await _db.Table<RecurringTransaction>().FirstAsync()).AmountCents.Should().Be(1575);
        (await MigrationRunner.GetVersionAsync(_db)).Should().Be(MigrationRunner.All.Count);
    }

    [Fact]
    public async Task RealDrift_IsRoundedToTheCent_AndSumsMatch()
    {
        // Values as REAL storage left them: 0.1 + 0.2 and 1.005 are not exact in binary floating point
        var stored = new[] { 0.30000000000000004, 1.0049999999999999, 19.999999999999996, -7.125, 0.0 };
        foreach (var amount in stored)
            await _db.ExecuteAsync("INSERT INTO \"Transaction\" (Description, Amount) VALUES ('x', ?)", amount);
        await _db.ExecuteAsync("INSERT INTO \"Transaction\" (Description, Amount) VALUES ('null', NULL)");

        await MigrateAsync();

        var cents = (await _db.Table<Transaction>().OrderBy(t => t.Id).ToListAsync()).Select(t => t.AmountCents).ToList();
        // Same cents as the old app showed: it read REAL through (decimal)double (15 significant digits)
        cents.Should().Equal(30, 101, 2000, -713, 0, 0);
        (await _db.Table<Transaction>().ToListAsync()).Sum(t => t.Amount).Should().Be(14.18m);
    }

    [Fact]
    public async Task BacksUpTheV1DatabaseFirst()
    {
        await _db.ExecuteAsync("INSERT INTO \"Transaction\" (Description, Amount) VALUES ('Lunch', 12.34)");

        await MigrateAsync();

        File.Exists(MigrationRunner.GetBackupPath(_dbPath, 2)).Should().BeTrue();
    }

    [Fact]
    public async Task TableMissingInTheOldInstall_IsSkipped()
    {
        // An install that predates budgets: CreateTableAsync makes Budget fresh, without a LimitAmount column
        await _db.ExecuteAsync("DROP TABLE \"Budget\"");
        await _db.ExecuteAsync("INSERT INTO \"Transaction\" (Description, Amount) VALUES ('Lunch', 12.34)");

        await MigrateAsync();

        (await _db.Table<Transaction>().FirstAsync()).Amount.Should().Be(12.34m);
        (await MigrationRunner.GetVersionAsync(_db)).Should().Be(MigrationRunner.All.Count);
    }

    [Fact]
    public async Task NewRowsAfterMigration_AreStoredInCents()
    {
        await MigrateAsync();

        await _db.InsertAsync(new Transaction { Description = "Coffee", Amount = 3.5m });

        var cents = await _db.ExecuteScalarAsync<long>("SELECT AmountCents FROM \"Transaction\" WHERE Description = 'Coffee'");
        cents.Should().Be(350);
    }

    [Fact]
    public async Task AccountsWithoutCurrency_GetTheBaseCurrency_OthersKeepTheirs()
    {
        await _db.ExecuteAsync("INSERT INTO \"Account\" (Name, InitialBalance) VALUES ('Old', 10)");
        await MigrateAsync(); // adds the CurrencyCode column (NULL for the old row) and runs migration 3
        await _db.ExecuteAsync("INSERT INTO \"Account\" (Name, CurrencyCode) VALUES ('Euros', 'EUR')");
        await _db.ExecuteAsync("UPDATE \"Account\" SET CurrencyCode = '' WHERE Name = 'Old'");

        await _db.RunInTransactionAsync(conn => M003_AccountCurrencyCodes.FillMissing(conn, "COP"));

        var accounts = await _db.Table<Account>().ToListAsync();
        accounts.Single(a => a.Name == "Old").CurrencyCode.Should().Be("COP");
        accounts.Single(a => a.Name == "Euros").CurrencyCode.Should().Be("EUR");
    }

    [Fact]
    public async Task Migration3_StampsOldAccounts()
    {
        await _db.ExecuteAsync("INSERT INTO \"Account\" (Name, InitialBalance) VALUES ('Old', 10)");

        await MigrateAsync();

        var old = await _db.Table<Account>().FirstAsync();
        old.CurrencyCode.Should().NotBeNullOrEmpty()
            .And.Be(MoneyRecord.Helpers.MoneyFormatter.ResolveCurrencyCode(null));
    }

    private Task MigrateAsync() =>
        DatabaseInitializer.CreateSchemaAsync(_db, new MigrationRunner(MigrationRunner.All));
}

public class MoneyAsCentsConversionTests
{
    [Theory]
    [InlineData(1.0049999999999999)]
    [InlineData(0.30000000000000004)]
    [InlineData(2.675)]
    [InlineData(-7.125)]
    [InlineData(1250.5)]
    public void ToCents_MatchesWhatTheOldAppShowed(double stored)
    {
        // The old read path was (decimal)double; new input of that value must get the same cents
        M002_MoneyAsCents.ToCents(stored).Should().Be(MoneyRecord.Helpers.Money.ToCents((decimal)stored));
    }

    [Fact]
    public void ToCents_NullAndNaN_AreZero()
    {
        M002_MoneyAsCents.ToCents(null).Should().Be(0);
        M002_MoneyAsCents.ToCents(double.NaN).Should().Be(0);
    }

    [Fact]
    public void ToCents_AbsurdLegacyValues_AreClampedInsteadOfFailing()
    {
        M002_MoneyAsCents.ToCents(1e30).Should().Be(99_999_999_999_999);
        M002_MoneyAsCents.ToCents(double.NegativeInfinity).Should().Be(-99_999_999_999_999);
    }
}
