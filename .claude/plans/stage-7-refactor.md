# Stage 7: Architecture Refactor

**Status**: In progress (7.1, 7.2 done)
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
- Done as `MoneyRecord.Core` (net10.0, `RootNamespace` MoneyRecord, so namespaces are unchanged). Moved: models, helpers,
  repositories + migrations, business services, `LocalizationService`, `IPreferencesService` and the `.resx` strings
  (`AppResources` is now public, `PublicResXFileCodeGenerator`). App keeps NavigationService, PreferencesService,
  ErrorHandler/`IErrorHandler`, converters and behaviors. `IAppPaths` + `AppPaths(appDataDirectory)` give the database,
  receipts and backups paths (`DatabaseInitializer`, `DataExportService`, `AddTransactionViewModel`, `MainViewModel`);
  the static `ReceiptFiles.Folder` is gone. `CurrencyMaskBehavior.ParseAmount` moved to `Helpers/AmountParser.Parse`.
  `Account.CurrencySymbol` was unused and is deleted. XAML `models:`/`helpers:` xmlns now carry `;assembly=MoneyRecord.Core`.
  CI test job runs on `ubuntu-latest` without workloads.
- **Deviations:** (1) Core references `CommunityToolkit.Mvvm` (plain .NET, MIT) so `BudgetProgress`, `TransactionGroup`,
  `AccountIcon`, `CategoryIcon` move unchanged; 7.3 reshapes `TransactionGroup`. (2) `Models/CategoryReport.cs`
  (`CategoryExpenseData`/`CategoryIncomeData`/`HeatmapCell`, MAUI `Color`) stays in the app: 7.4 replaces it with
  `CategoryAmount` + a heatmap level, so converting its colours now would be thrown away. (`BudgetProgress` already uses
  `BudgetLevel` since 6.6.)

### 7.2 Dialogs and navigation behind interfaces
- New `IDialogService` (`AlertAsync`, `ConfirmAsync`, `PromptAsync`, `ActionSheetAsync`, `ToastAsync`).
  Replace every `Shell.Current.DisplayAlertAsync/DisplayPromptAsync/DisplayActionSheetAsync` and `Toast.Make` in ViewModels.
- All navigation through `INavigationService`; remove direct `Shell.Current.GoToAsync` from ViewModels.
- Fix `NavigationService.RootRoutes` (`NavigationService.cs:16`): add BudgetPage, TrendsReportPage,
  RecurringTransactionsPage, SettingsPage, or derive root routes from `AppShell` items.
- Done as `IDialogService` + `INavigationService` in Core (`Services/Interfaces`, no MAUI types; the prompt takes
  `numeric: bool` instead of `Keyboard`, the action sheet returns null on cancel). App: `DialogService` (Shell dialogs +
  CommunityToolkit toast, on the main thread) and a thin `NavigationService`. `ErrorHandler`/`IErrorHandler` moved to
  Core and show errors through `IDialogService`. No ViewModel uses `Shell.Current` or `Toast` any more.
  Root routes are derived instead of listed: Core `NavigationHistory` treats any one-segment absolute location
  (`//BudgetPage`) as a root, so every flyout page clears the history and new flyout pages need no list entry.
  Only AppShell's `Navigated` event records history (`NavigateToAsync` recorded it too, so each push was stored
  twice). `MainActivity` hands the back gesture to `Shell.SendBackButtonPressed()` → `AppShell.OnBackButtonPressed`
  (one back path); a pushed page opened from a report now goes back to that report instead of Home. Removed the dead
  `OnBackButtonPressed` overrides on five pages and the unbound `GoBack` commands in ManageAccounts/ManageCategories.
  Tests: `NavigationHistoryTests`, `ErrorHandlerTests` (fake `IDialogService`).
- **Deviation:** ViewModel tests with a fake `IDialogService` (plan "Tests") need the ViewModels out of the MAUI
  project; the interfaces are in Core now, so those tests can come once ViewModels move (7.3 `MainViewModel` split is
  the first candidate).

### 7.3 Split `MainViewModel` (747 lines)
- `ILedgerQueryService` → returns `LedgerEntry` records (transactions + transfers, already enriched) for a range.
  Replaces `ConvertTransfersToTransactions` and its fake negative IDs (`-transfer.Id - 1000000`).
- `LedgerEntry` (Core): `Kind` (Income/Expense/TransferOut/TransferIn/Transfer), `SourceId`, amounts in minor units.
- `TransactionFilter` (record) + pure `LedgerFilter.Apply(entries, filter)`, filtering by IDs/enums.
- `IBudgetSummaryService.GetSummaryAsync(period)` shared by Home and the Budget page (fixes P5: Home must use
  the same period as the Budget page, or say "this month").

### 7.4 Remove duplication
- One `CategoryReportViewModel(CategoryType)` + one `CategoryReportPage`; delete Expense/Income copies and
  `CategoryExpenseData`/`CategoryIncomeData` → `CategoryAmount` (in Core; heatmap colour → a level mapped by a converter,
  so `Models/CategoryReport.cs` leaves the app — deferred from 7.1).
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
