# Stage 1: Core Improvements

**Status**: Complete
**Depends on**: Stage 0 complete
**Complexity**: Medium

## Goal
Performance optimization, UI/UX polish, centralized error handling, data validation, and budget feature enhancement.

## Tasks

### 1.1 Performance — DONE

**1.1.1 In-Memory Caching** — DONE
- Added caching directly in `CategoryRepository` (`_allCache`, `_typeCache`) and `AccountRepository` (`_cache`)
- Cache invalidated on `SaveAsync`/`DeleteAsync`
- No separate `ICachingService` needed — repositories are already singletons

**1.1.2 Batch Database Writes** — DONE
- Wrapped `ReassignCategoryAsync` and `ReassignAccountAsync` in `RunInTransactionAsync`

**1.1.3 Fix N+1 in Balance Calculations** — DONE
- Refactored `GetAllAccountBalancesAsync` to load all accounts, transactions, and transfers in 3 parallel queries, then compute per-account via dictionary lookups

### 1.2 UI/UX Polish — PARTIAL

**1.2.1 Toast Notifications** — DONE
- Added `CommunityToolkit.Maui` 15.0.1 NuGet package
- Added `.UseMauiCommunityToolkit()` in `MauiProgram.cs`
- Replaced success `DisplayAlertAsync` with `Toast.Make()` in 6 ViewModels

**1.2.2 Page Transitions** — DEFERRED (Stage 2+)
**1.2.3 Loading Skeletons** — DEFERRED (Stage 2+)

### 1.3 Centralized Error Handling — DONE
- Created `Services/Interfaces/IErrorHandler.cs` and `Services/ErrorHandler.cs`
- Logs via `Debug.WriteLine`, shows via `MainThread.InvokeOnMainThreadAsync` + `DisplayAlertAsync`
- Replaced all scattered error patterns across all 9 ViewModels

### 1.4 Data Validation Layer — DEFERRED
- Existing ad-hoc validation in ViewModels is adequate for current scope
- Would be over-engineering without more complex entity creation flows

### 1.5 Budget Enhancements — DONE

**1.5.1 Budget Warning on AddTransactionPage** — DONE
- Added `IBudgetRepository` to `AddTransactionViewModel`
- Added `ShowBudgetWarning`/`BudgetWarningText` observable properties
- `CheckBudgetAsync()` fires on category selection change for expense transactions
- Orange warning banner in `AddTransactionPage.xaml` with `BudgetExceeded`/`BudgetNearLimit` messages
- Localized strings added (en + es)

**1.5.2 Budget Summary on Dashboard** — DONE
- Added `IBudgetRepository` and `ICategoryRepository` to `MainViewModel`
- Added `BudgetSummaries` (top 3 by progress %), `HasBudgets`, `BudgetsOnTrack`, `BudgetsOverBudget` properties
- `LoadBudgetSummaryAsync()` runs within `LoadDataAsync()` using each budget's native period
- Compact summary card in `MainPage.xaml` with category icons, progress bars, and on-track/over-budget pill badges
- Added `IntToBoolConverter`, reused existing `PercentageToProgressConverter`

## Verification
- Build: `dotnet build -f net10.0-android` — 0 errors (only NU1903 upstream SQLitePCLRaw warnings)
