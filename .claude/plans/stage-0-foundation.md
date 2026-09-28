# Stage 0: Foundation & Cleanup

**Status**: Complete ✅
**Priority**: CRITICAL — blocks all other stages
**Complexity**: Medium (mostly mechanical refactoring)

## Goal
Complete the repository migration, fix bugs, clean dead code, and establish project infrastructure (CLAUDE.md, skills).

## Tasks

### 0.1 Create CLAUDE.md ✅
- Created at project root with build commands, architecture map, patterns, conventions

### 0.2 Fix Budget table initialization bug ✅
- **File**: `Services/Repositories/DatabaseInitializer.cs`
- **Change**: Added `await Database.CreateTableAsync<Budget>();` after Transfer table creation

### 0.3 Create IBudgetRepository + BudgetRepository ✅
- **Created**: `Services/Interfaces/IBudgetRepository.cs` — extends `IRepository<Budget>`, adds `GetActiveBudgetsAsync`, `GetByCategoryIdAsync`, `UpdateAmountAsync`, `DeleteByIdAsync`
- **Created**: `Services/Repositories/BudgetRepository.cs` — sealed class, same pattern as other repos
- **Modified**: `MauiProgram.cs` — registered `IBudgetRepository, BudgetRepository` in DI

### 0.4 Add GetCategoryExpensesAsync to IBalanceService ✅
- Added to `Services/Interfaces/IBalanceService.cs` and implemented in `Services/BalanceService.cs`

### 0.5 Migrate all 9 ViewModels from DatabaseService to repositories ✅
All ViewModels now use proper repository/service interfaces:

| ViewModel | New Dependencies |
|---|---|
| MainViewModel | IBalanceService, ITransactionEnrichmentService, ITransactionRepository, ITransferRepository, IAccountRepository |
| AddTransactionViewModel | IAccountRepository, ICategoryRepository, IBalanceService, ITransactionRepository |
| AddTransferViewModel | IAccountRepository, ITransferRepository, IBalanceService |
| BudgetViewModel | ICategoryRepository, IBudgetRepository, IBalanceService |
| ExpenseReportViewModel | ITransactionEnrichmentService |
| IncomeReportViewModel | ITransactionEnrichmentService |
| ManageCategoriesViewModel | ICategoryRepository, ITransactionRepository |
| ManageAccountsViewModel | IAccountRepository, ITransactionRepository |
| TransfersViewModel | ITransactionEnrichmentService, ITransferRepository |

**Notable**: ManageAccountsViewModel now has explicit transaction reassignment logic (was hidden in DatabaseService.DeleteAccountAsync).

### 0.6 Delete legacy DatabaseService ✅
- **Deleted**: `Services/DatabaseService.cs` (668 lines)
- **Modified**: `MauiProgram.cs` — removed singleton registration
- Verified zero remaining references via grep

### 0.7 Clean ghost .csproj references ✅
- **Removed** `MauiXaml` entries for non-existent files:
  - `Views\MainPage_New.xaml`
  - `Views\ManageCategoriesPage_New.xaml`
  - `Views\TransferPage.xaml`

### 0.8 Fix missing Spanish translations ✅
- **Added** 6 missing keys to `Resources/Strings/AppResources.es.resx`:
  - BudgetPeriod, OriginalLimit, ProjectedLimit, PerDay, PerMonth, PerYear

## Verification ✅
- Build: `dotnet build MoneyRecord/MoneyRecord.csproj -f net10.0-android` — **0 errors**, warnings only (NuGet vulnerability on SQLitePCLRaw, unused variables, obsolete DisplayAlert)
- All ViewModels compile against repository interfaces
- No references to deleted DatabaseService remain

## Remaining Warnings (non-blocking, addressable in Stage 1)
- NU1903: SQLitePCLRaw.lib.e_sqlite3 vulnerability — update to latest version
- CS0618: `DisplayAlert` obsolete, use `DisplayAlertAsync` — in `MainPage.xaml.cs:36`
- CS0168: Unused `ex` variable — in `MainViewModel.cs:193`
- CS0067: Unused `PropertyChanged` events — in `PeriodHelper.cs:23,52`
