---
name: test
description: Create and run unit tests using xUnit with mocked repositories
triggers:
  - "test"
  - "unit test"
  - "write test"
  - "run tests"
  - "test coverage"
---

# Test Skill — Create & Run Unit Tests

## Before Writing Tests

1. **CodeGraph first**: Query `codegraph_explore` with the symbol under test to understand:
   - Its dependencies (what it injects via constructor)
   - Its callers (what depends on it — tests should cover the same scenarios)
   - Its blast radius (what breaks if it changes)

2. **Check existing tests**: Look in `MoneyRecord.Tests/` for existing test patterns and conventions.

## Test Project Setup

`MoneyRecord.Tests/` exists (xUnit, NSubstitute, **AwesomeAssertions**: `using AwesomeAssertions;`). It targets
`net10.0-windows10.0.19041.0`, so run it from PowerShell on Windows. Package versions are pinned: don't use wildcards,
and never add FluentAssertions (v8 is non-commercial only; this project is AGPL). If tests fail with `0x800711C7`, see the
Smart App Control note in the `run-moneyrecord` skill.

## Test Conventions

### Naming
- Test class: `{ClassUnderTest}Tests` (e.g., `BalanceServiceTests`)
- Test method: `{MethodName}_Should{ExpectedBehavior}_When{Condition}` (e.g., `GetTotalBalance_ShouldSumAllAccounts_WhenMultipleAccountsExist`)
- File location: Mirror source structure (e.g., `MoneyRecord.Tests/Services/BalanceServiceTests.cs`)

### Structure (AAA Pattern)
```csharp
[Fact]
public async Task MethodName_ShouldDoX_WhenY()
{
    // Arrange
    var mockRepo = Substitute.For<IAccountRepository>();
    mockRepo.GetAllAsync().Returns(new List<Account> { ... });
    var sut = new BalanceService(mockRepo, ...);

    // Act
    var result = await sut.GetTotalBalanceAsync();

    // Assert
    result.Should().Be(expectedValue);
}
```

### What to Mock
- Repository interfaces (`IAccountRepository`, `ICategoryRepository`, etc.)
- `IPreferencesService` for settings
- `INavigationService` for navigation calls
- Do NOT mock `BalanceService`, `TransactionEnrichmentService` — test them with mocked repos
- Database code (repositories, migrations, cascades, backup/restore): use a real temp SQLite file through
  `new DatabaseInitializer(path)` instead of mocks (see `MigrationRunnerTests`, `DeleteCascadeTests`, `BackupRestoreTests`)
- Culture-dependent code: pass an explicit `CultureInfo` (en-US, es-ES, es-CO, ja-JP) rather than relying on the machine's
- Localized text: compare with `LocalizationService.Instance.GetString(key)`, not an English literal

### Priority Test Targets
1. **Money and currency**: `Money`, `MoneyFormatter`, `CurrencyService` (cents, symbols, zero-decimal currencies)
2. **Migrations**: every new `IMigration`, against a real database from the previous schema version
3. **Data safety**: delete cascades, backup/restore round trips and failure paths
4. **BalanceService** / **TransactionEnrichmentService**: balances, per-currency subtotals, joins
5. **Pure helpers**: `BudgetProjectionHelper`, `DateRangeHelper`, `DateFormatter`, `Plurals`
6. **CurrencyMaskBehavior.ParseAmount**: parsing per culture and currency
7. **ViewModels**: command behavior with mocked dependencies (Stage 7 makes this easier)

### Running Tests
```bash
dotnet test MoneyRecord.Tests/MoneyRecord.Tests.csproj
dotnet test MoneyRecord.Tests/MoneyRecord.Tests.csproj --filter "FullyQualifiedName~BalanceService"
```
