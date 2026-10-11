# Stage 7: Architecture Refactor

**Status**: Pending
**Depends on**: Stage 6 (money format + migrations must land first)
**Complexity**: High (wide but mechanical; no user-visible change)
**Source**: Product & engineering review, 2026-10-11 (engineering findings)

## Goal
Smaller, testable code: domain logic in a plain .NET library, ViewModels free of `Shell.Current`,
no duplicated business rules. Behaviour must not change; the device check before and after must match.

## Tasks

### 7.1 `MoneyRecord.Core` class library (`net10.0`)
- Move `Models/` (entities + DTOs), `Helpers/` (DateRange, PeriodHelper, BudgetProjection, IconHelper),
  `Services/Interfaces/`, repositories, and business services into `MoneyRecord.Core/`.
- Remove MAUI types from Core: `Color` in `BudgetProgress` / `CategoryReport` → a `BudgetStatus` / intensity enum,
  mapped to colours by converters in the app. `Account.CurrencySymbol` stops calling static `CurrencyService`.
- `FileSystem.AppDataDirectory` → inject `IAppPaths` (app implements it; tests use a temp dir).
- Re-target `MoneyRecord.Tests` to plain `net10.0` referencing Core only → tests run on Linux; CI test job drops the
  MAUI workload install.

### 7.2 Dialogs and navigation behind interfaces
- New `IDialogService` (`AlertAsync`, `ConfirmAsync`, `PromptAsync`, `ActionSheetAsync`, `ToastAsync`).
  Replace every `Shell.Current.DisplayAlertAsync/DisplayPromptAsync/DisplayActionSheetAsync` and `Toast.Make` in ViewModels.
- All navigation through `INavigationService`; remove direct `Shell.Current.GoToAsync` from ViewModels.
- Fix `NavigationService.RootRoutes` (`NavigationService.cs:16`): add BudgetPage, TrendsReportPage,
  RecurringTransactionsPage, SettingsPage, or derive root routes from `AppShell` items.

### 7.3 Split `MainViewModel` (747 lines)
- `ILedgerQueryService` → returns `LedgerEntry` records (transactions + transfers, already enriched) for a range.
  Replaces `ConvertTransfersToTransactions` and its fake negative IDs (`-transfer.Id - 1000000`).
- `LedgerEntry` (Core): `Kind` (Income/Expense/TransferOut/TransferIn/Transfer), `SourceId`, amounts in minor units.
- `TransactionFilter` (record) + pure `LedgerFilter.Apply(entries, filter)`, filtering by IDs/enums.
- `IBudgetSummaryService.GetSummaryAsync(period)` shared by Home and the Budget page (fixes P5: Home must use
  the same period as the Budget page, or say "this month").

### 7.4 Remove duplication
- One `CategoryReportViewModel(CategoryType)` + one `CategoryReportPage`; delete Expense/Income copies and
  `CategoryExpenseData`/`CategoryIncomeData` → `CategoryAmount`.
- Budget period window: one `DateRangeHelper.GetBudgetWindow(BudgetPeriod, DateTime now)`; used by MainViewModel,
  AddTransactionViewModel, BudgetViewModel.
- Recurrence: one `RecurrenceCalculator.Next(current, frequency, dayOfMonth)`; `AddTransactionViewModel.cs:366`
  currently drops the time of day while `RecurringTransactionService` keeps it. Pick one rule (keep date, time 00:00) and test it.
- Icon hex parsing only in `IconHelper`.
- Split `Converters/Converters.cs` (731 lines) one-per-file; replace duplicates with CommunityToolkit converters
  (`InvertedBoolConverter`, `IsStringNotNullOrEmptyConverter`, `IsNotNullConverter`, …).

### 7.5 Data access
- `RepositoryBase<T>` holding `EnsureInitializedAsync` + CRUD; `DataExportService` uses repositories.
- SQL aggregates in `BalanceService`: `SELECT AccountId, Type, SUM(AmountMinor) … GROUP BY` instead of loading all rows.
- Budget loading: one query for categories + one grouped spend query (no per-budget round trips).
- `IUnitOfWork` / `RunInTransactionAsync` helper used by the Stage 6 cascades.

### 7.6 Async correctness
- Property-changed handlers stop calling `_ = LoadDataAsync()`; use a single `AsyncRelayCommand` with
  cancellation (`IncludeCancelCommand`) so a new period cancels the previous load.
- Debounce search text (300 ms) before filtering.
- `ErrorHandler`: log with `ILogger` (Serilog file sink in Stage 9); show friendly messages, not `ex.Message`.

## Tests
- Core unit tests for LedgerFilter, RecurrenceCalculator, GetBudgetWindow, BalanceService SQL sums (temp SQLite).
- ViewModel tests with a fake `IDialogService` / `INavigationService` (delete confirm, save validation).
- Target: every service in Core has tests; CI runs them on `ubuntu-latest`.

## Verify on device (`/run-moneyrecord`)
Take the same screenshot set before and after (home, menu, accounts, add expense, budget, reports, Spanish).
They must match apart from intended fixes.

## Done when
`MoneyRecord.Tests` targets `net10.0` and runs on Linux; no ViewModel references `Shell.Current`; the
duplicated report/period/recurrence code is gone; `MainViewModel` is under ~300 lines.
