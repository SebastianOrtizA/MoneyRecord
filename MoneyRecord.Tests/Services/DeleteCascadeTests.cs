using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Repositories;
using SQLite;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>
/// AccountService / CategoryService against a real temp SQLite file (seeded like a new install:
/// the default Cash account plus the default categories).
/// </summary>
public sealed class DeleteCascadeTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"moneyrecord-test-{Guid.NewGuid():N}.db3");
    private DatabaseInitializer _dbInitializer = null!;
    private AccountRepository _accountRepository = null!;
    private CategoryRepository _categoryRepository = null!;
    private AccountService _accountService = null!;
    private CategoryService _categoryService = null!;

    private SQLiteAsyncConnection Db => _dbInitializer.Database!;

    public async Task InitializeAsync()
    {
        _dbInitializer = new DatabaseInitializer(_dbPath);
        await _dbInitializer.InitializeAsync();
        _accountRepository = new AccountRepository(_dbInitializer);
        _categoryRepository = new CategoryRepository(_dbInitializer);
        _accountService = new AccountService(_dbInitializer, _accountRepository);
        _categoryService = new CategoryService(_dbInitializer, _categoryRepository);
    }

    public async Task DisposeAsync()
    {
        await Db.CloseAsync();
        foreach (var file in Directory.GetFiles(Path.GetDirectoryName(_dbPath)!, Path.GetFileName(_dbPath) + "*"))
            File.Delete(file);
    }

    // ---- Accounts ----

    [Fact]
    public async Task DeleteAccount_MovesTransactionsAndRecurringRulesToDefault()
    {
        var cash = await DefaultAccountAsync();
        var bank = await AddAccountAsync("Bank");
        await Db.InsertAsync(new Transaction { Description = "Lunch", Amount = 10m, AccountId = bank.Id });
        await Db.InsertAsync(new RecurringTransaction { Description = "Rent", Amount = 500m, AccountId = bank.Id });

        await _accountService.DeleteAsync(bank.Id);

        (await Db.FindAsync<Account>(bank.Id)).Should().BeNull();
        (await Db.Table<Transaction>().FirstAsync()).AccountId.Should().Be(cash.Id);
        (await Db.Table<RecurringTransaction>().FirstAsync()).AccountId.Should().Be(cash.Id);
    }

    [Fact]
    public async Task DeleteAccount_WithTransfers_IsRefusedAndChangesNothing()
    {
        var cash = await DefaultAccountAsync();
        var bank = await AddAccountAsync("Bank");
        await Db.InsertAsync(new Transaction { Description = "Lunch", Amount = 10m, AccountId = bank.Id });
        await Db.InsertAsync(new Transfer { Amount = 50m, SourceAccountId = cash.Id, DestinationAccountId = bank.Id });

        var usage = await _accountService.GetUsageAsync(bank.Id);
        var act = () => _accountService.DeleteAsync(bank.Id);

        usage.Transfers.Should().Be(1);
        await act.Should().ThrowAsync<InvalidOperationException>();
        (await Db.FindAsync<Account>(bank.Id)).Should().NotBeNull();
        (await Db.Table<Transaction>().FirstAsync()).AccountId.Should().Be(bank.Id, "the transaction must roll back");
    }

    [Fact]
    public async Task DeleteAccount_Default_IsRefused()
    {
        var cash = await DefaultAccountAsync();

        var act = () => _accountService.DeleteAsync(cash.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DeleteAccount_OtherCurrencyWithTransactions_IsRefusedAndChangesNothing()
    {
        var cash = await DefaultAccountAsync();
        var euros = await AddAccountAsync("Euros", OtherCurrencyThan(cash));
        await Db.InsertAsync(new Transaction { Description = "Croissant", Amount = 3m, AccountId = euros.Id });

        var act = () => _accountService.DeleteAsync(euros.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await Db.FindAsync<Account>(euros.Id)).Should().NotBeNull();
        (await Db.Table<Transaction>().FirstAsync()).AccountId.Should().Be(euros.Id);
    }

    [Fact]
    public async Task DeleteAccount_OtherCurrencyUnused_IsDeleted()
    {
        var cash = await DefaultAccountAsync();
        var euros = await AddAccountAsync("Euros", OtherCurrencyThan(cash));

        await _accountService.DeleteAsync(euros.Id);

        (await Db.FindAsync<Account>(euros.Id)).Should().BeNull();
    }

    private static string OtherCurrencyThan(Account account) =>
        MoneyRecord.Helpers.MoneyFormatter.ResolveCurrencyCode(account.CurrencyCode) == "EUR" ? "USD" : "EUR";

    [Fact]
    public async Task DeleteAccount_RefreshesTheRepositoryCache()
    {
        var bank = await AddAccountAsync("Bank");
        (await _accountRepository.GetAllAsync()).Should().Contain(a => a.Id == bank.Id);

        await _accountService.DeleteAsync(bank.Id);

        (await _accountRepository.GetAllAsync()).Should().NotContain(a => a.Id == bank.Id);
    }

    [Fact]
    public async Task AccountUsage_CountsEverything()
    {
        var cash = await DefaultAccountAsync();
        var bank = await AddAccountAsync("Bank");
        await Db.InsertAsync(new Transaction { AccountId = bank.Id });
        await Db.InsertAsync(new Transaction { AccountId = bank.Id });
        await Db.InsertAsync(new Transfer { SourceAccountId = bank.Id, DestinationAccountId = cash.Id });
        await Db.InsertAsync(new RecurringTransaction { AccountId = bank.Id });

        (await _accountService.GetUsageAsync(bank.Id)).Should().Be(new MoneyRecord.Services.Interfaces.AccountUsage(2, 1, 1));
    }

    // ---- Categories ----

    [Fact]
    public async Task DeleteCategory_MovesTransactionsRulesAndBudget()
    {
        var (food, transport) = await TwoExpenseCategoriesAsync();
        await Db.InsertAsync(new Transaction { Description = "Lunch", CategoryId = food.Id });
        await Db.InsertAsync(new RecurringTransaction { Description = "Groceries", CategoryId = food.Id });
        await Db.InsertAsync(new Budget { CategoryId = food.Id, LimitAmount = 300m });

        var result = await _categoryService.DeleteAsync(food.Id, transport.Id);

        result.Should().Be(new MoneyRecord.Services.Interfaces.CategoryDeleteResult(1, 1, 1, 0));
        (await Db.FindAsync<Category>(food.Id)).Should().BeNull();
        (await Db.Table<Transaction>().FirstAsync()).CategoryId.Should().Be(transport.Id);
        (await Db.Table<RecurringTransaction>().FirstAsync()).CategoryId.Should().Be(transport.Id);
        (await Db.Table<Budget>().FirstAsync()).CategoryId.Should().Be(transport.Id);
    }

    [Fact]
    public async Task DeleteCategory_ReplacementWithBudget_KeepsItsBudgetAndDropsTheOther()
    {
        var (food, transport) = await TwoExpenseCategoriesAsync();
        await Db.InsertAsync(new Budget { CategoryId = food.Id, LimitAmount = 300m });
        await Db.InsertAsync(new Budget { CategoryId = transport.Id, LimitAmount = 100m });

        var result = await _categoryService.DeleteAsync(food.Id, transport.Id);

        result.BudgetsDeleted.Should().Be(1);
        var budgets = await Db.Table<Budget>().ToListAsync();
        budgets.Should().ContainSingle().Which.LimitAmount.Should().Be(100m);
    }

    [Fact]
    public async Task DeleteCategory_InUseWithoutReplacement_IsRefused()
    {
        var (food, _) = await TwoExpenseCategoriesAsync();
        await Db.InsertAsync(new RecurringTransaction { CategoryId = food.Id });

        var act = () => _categoryService.DeleteAsync(food.Id, replacementId: null);

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await Db.FindAsync<Category>(food.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteCategory_Unused_DeletesItsBudgetToo()
    {
        var (food, _) = await TwoExpenseCategoriesAsync();
        await Db.InsertAsync(new Budget { CategoryId = food.Id, LimitAmount = 300m });

        var result = await _categoryService.DeleteAsync(food.Id, replacementId: null);

        result.BudgetsDeleted.Should().Be(1);
        (await Db.Table<Budget>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DeleteCategory_ReplacementOfAnotherType_IsRefusedAndChangesNothing()
    {
        var (food, _) = await TwoExpenseCategoriesAsync();
        var salary = (await _categoryRepository.GetByTypeAsync(CategoryType.Income)).First();
        await Db.InsertAsync(new Transaction { CategoryId = food.Id });

        var act = () => _categoryService.DeleteAsync(food.Id, salary.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await Db.Table<Transaction>().FirstAsync()).CategoryId.Should().Be(food.Id);
    }

    [Fact]
    public async Task DeleteCategory_RefreshesTheRepositoryCache()
    {
        var (food, _) = await TwoExpenseCategoriesAsync();

        await _categoryService.DeleteAsync(food.Id, replacementId: null);

        (await _categoryRepository.GetAllAsync()).Should().NotContain(c => c.Id == food.Id);
    }

    private async Task<Account> DefaultAccountAsync() => (await _accountRepository.GetDefaultAsync())!;

    private async Task<Account> AddAccountAsync(string name, string currencyCode = "")
    {
        // No currency code = the base currency, the same as the seeded Cash account's
        var account = new Account { Name = name, CurrencyCode = currencyCode };
        await _accountRepository.SaveAsync(account);
        return account;
    }

    private async Task<(Category, Category)> TwoExpenseCategoriesAsync()
    {
        var expenses = await _categoryRepository.GetByTypeAsync(CategoryType.Expense);
        return (expenses[0], expenses[1]);
    }
}
