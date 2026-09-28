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

If the test project doesn't exist yet:
```bash
dotnet new xunit -n MoneyRecord.Tests -o MoneyRecord.Tests
dotnet sln MoneyRecord.slnx add MoneyRecord.Tests/MoneyRecord.Tests.csproj
dotnet add MoneyRecord.Tests/MoneyRecord.Tests.csproj reference MoneyRecord/MoneyRecord.csproj
dotnet add MoneyRecord.Tests/MoneyRecord.Tests.csproj package NSubstitute
dotnet add MoneyRecord.Tests/MoneyRecord.Tests.csproj package FluentAssertions
```

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

### Priority Test Targets
1. **BalanceService** — All balance calculations (most critical business logic)
2. **BudgetProjectionHelper** — Pure static methods, easy to test, complex math
3. **DateRangeHelper** — Pure static methods, date edge cases
4. **TransactionEnrichmentService** — Enrichment/join logic
5. **Validators** (when created) — Input validation rules
6. **CurrencyMaskBehavior.ParseCurrencyValue** — Currency parsing edge cases
7. **ViewModels** — Command behavior with mocked dependencies

### Running Tests
```bash
dotnet test MoneyRecord.Tests/MoneyRecord.Tests.csproj
dotnet test MoneyRecord.Tests/MoneyRecord.Tests.csproj --filter "FullyQualifiedName~BalanceService"
```
