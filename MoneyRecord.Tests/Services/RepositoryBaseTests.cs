using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Repositories;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>The shared create/read/update/delete of <see cref="RepositoryBase{T}"/> against a temp SQLite file.</summary>
public sealed class RepositoryBaseTests : IAsyncLifetime
{
    private readonly string _appData = Path.Combine(Path.GetTempPath(), $"moneyrecord-repo-{Guid.NewGuid():N}");
    private DatabaseInitializer _dbInitializer = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_appData);
        _dbInitializer = new DatabaseInitializer(new AppPaths(_appData));
        await _dbInitializer.GetConnectionAsync();
    }

    public async Task DisposeAsync()
    {
        await (await _dbInitializer.GetConnectionAsync()).CloseAsync();
        Directory.Delete(_appData, recursive: true);
    }

    [Fact]
    public async Task Save_inserts_a_new_row_then_updates_it()
    {
        var repository = new BudgetRepository(_dbInitializer);
        var budget = new Budget { CategoryId = 3, LimitAmount = 100m, IsActive = true };

        await repository.SaveAsync(budget);
        budget.Id.Should().NotBe(0);

        budget.LimitAmount = 80m;
        await repository.SaveAsync(budget);

        var all = await repository.GetAllAsync();
        all.Should().ContainSingle().Which.LimitAmount.Should().Be(80m);
        (await repository.GetByIdAsync(budget.Id))!.LimitAmount.Should().Be(80m);
    }

    [Fact]
    public async Task Delete_by_id_removes_the_row_and_an_unknown_id_deletes_nothing()
    {
        var repository = new RecurringTransactionRepository(_dbInitializer);
        var rule = new RecurringTransaction { Description = "Rent", Amount = 500m, IsActive = true };
        await repository.SaveAsync(rule);

        (await repository.DeleteByIdAsync(rule.Id)).Should().Be(1);
        (await repository.DeleteByIdAsync(rule.Id)).Should().Be(0);
        (await repository.GetByIdAsync(rule.Id)).Should().BeNull();
    }

    [Fact]
    public async Task A_cached_repository_sees_its_own_writes()
    {
        var repository = new AccountRepository(_dbInitializer);
        var before = (await repository.GetAllAsync()).Count; // fills the cache

        await repository.SaveAsync(new Account { Name = "Savings" });

        (await repository.GetAllAsync()).Should().HaveCount(before + 1);
    }

    [Fact]
    public async Task UpdateAmount_and_Deactivate_change_one_field_of_an_existing_row()
    {
        var budgets = new BudgetRepository(_dbInitializer);
        var budget = new Budget { CategoryId = 3, LimitAmount = 100m, IsActive = true };
        await budgets.SaveAsync(budget);
        var rules = new RecurringTransactionRepository(_dbInitializer);
        var rule = new RecurringTransaction { Description = "Gym", IsActive = true };
        await rules.SaveAsync(rule);

        (await budgets.UpdateAmountAsync(budget.Id, 60m)).Should().Be(1);
        (await budgets.UpdateAmountAsync(999, 60m)).Should().Be(0);
        (await rules.DeactivateAsync(rule.Id)).Should().Be(1);

        (await budgets.GetByIdAsync(budget.Id))!.LimitAmount.Should().Be(60m);
        (await rules.GetActiveAsync()).Should().BeEmpty();
    }
}
