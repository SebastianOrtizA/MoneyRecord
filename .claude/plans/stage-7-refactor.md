# Stage 7: Architecture Refactor

**Status**: Done (7.1–7.6)
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
  A go-back in progress ignores a second back press (`BeginGoBack` returns null) and a failed one is cancelled;
  AppShell catches back failures (async void). The Android back callback re-enables itself after closing the app.
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
- Done as `LedgerEntry` (Core record: `Kind`, `SourceId` = the real Transaction/Transfer ID, `AmountCents`,
  `SignedAmount`, category/account IDs + names, `CounterpartAccountId` for transfers) and `LedgerQueryService`
  (on top of `TransactionEnrichmentService`; `transfersPerAccount` splits a transfer into TransferOut/TransferIn for
  the account view). No more fake negative IDs: Edit/Delete use `SourceId`, and Edit loads the `Transaction` by ID.
  `TransactionFilter` record + `LedgerFilter.Apply/CategoryOptions/AccountOptions`: category and account pickers hold
  `FilterOption(Id, Name)`, an account filter matches both accounts of a whole transfer but only the own half of a split one.
  `LedgerGrouping.Group` builds `TransactionGroup`s (now a plain container; groups are keyed by category/account ID, so
  same-named income and expense categories, or a user category named like "Transfers", stay apart; balance and icon
  looked up by ID; default icon codes only in `IconHelper`). `BudgetSummaryService` (one category query instead of one per budget) is used by the Budget
  page and the Home card; the Home card uses **Home's period** with projected limits, so on the default "Calendar
  month" both pages show the same numbers (a $10/day budget was compared with today's spending only on Home).
  Removed `Transaction.TransferId/IsOutgoingTransfer/TransferCounterpartAccount` and three unused Manage* commands.
  Sorting no longer reloads from the database, and changing the grouping reloads only when entering or leaving the
  account view (the only one that splits transfers). The min/max filters parse with `AmountParser`, like amount entry.
  Tests: `LedgerFilterTests`, `LedgerGroupingTests`, `LedgerQueryServiceTests`, `BudgetSummaryServiceTests` (248 pass).
- **Deviations:** (1) "Transfers" is no longer an entry in the Home category filter (transfers have no category ID;
  the Type filter selects them). (2) `MainViewModel` is 446 lines (was 795): what is left is mostly
  `[ObservableProperty]` fields and the commands; the ~300 target is for the end of the stage (7.6 replaces the
  property-changed reloads). ViewModel tests still wait for ViewModels to move to Core (they use `MainThread` and page types).

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
- Done as: **Reports** — Core `CategoryReportBuilder.Build(transactions, CategoryType)` → `CategoryReport(Total,
  Categories)` of `CategoryAmount` (type, ID, name, icon, amount, share, `HeatLevel` 0–11, `IsMinorShare` < 10%); groups by
  category ID. One `CategoryReportViewModel` (`Type` → `Texts`, `IsIncome`) and one `CategoryReportPage`;
  `ExpenseReportPage`/`IncomeReportPage` are code-only subclasses that set the type, so AppShell routes and DI keys stay.
  Type-dependent colours are page styles with an `IsIncome` DataTrigger; `HeatmapColorConverter` maps a level (or the
  legend's Low/Medium/High) to the expense or income palette, `Text=True` gives the contrast colour. One details command
  instead of two; the popup dates use `DateFormatter` (was a fixed `MMM dd, yyyy`). `Models/CategoryReport.cs` and the
  app's `Models/` folder are gone. **Budget window** — `DateRangeHelper.GetBudgetWindow(period, now)` (year now ends at
  the last tick, not 23:59:59); only `AddTransactionViewModel` still needed one (7.3 moved the others to
  `BudgetSummaryService`, which uses the selected period). **Recurrence** — `RecurrenceCalculator.Next` (service, new
  recurring) and `NextAfter` (editing a due recurrence); occurrences are dates at 00:00, and a clamped day (31 → Feb 28)
  returns to the 31st next month (`NextAfter` used `AddMonths` on the clamped date and stayed on the 28th). A monthly
  recurrence without a chosen day keeps its start date's day (`RecurringTransaction.EffectiveDayOfMonth`), so it no
  longer drifts Jan 31 → Feb 28 → Mar 28.
  **Icons** — no hex parsing outside `IconHelper`. **Converters** — one file each; unused `StringEquals`,
  `IconSelectionColor`, `IconSelectionBackground` deleted; `InvertedBool`, `IntToBool`, `StringToBool` →
  CommunityToolkit (`InvertedBoolConverter`, `IntToBoolConverter`, `IsStringNotNullOrEmptyConverter`) with
  `SetShouldSuppressExceptionsInConverters(true)` so a null input doesn't throw. Tests: `CategoryReportBuilderTests`,
  `RecurrenceCalculatorTests`, `GetBudgetWindow` (268 pass).
- **Visible changes:** the income legend's "Medium" label is black (contrast rule) instead of white; the expense legend's
  border now also frames the income "Low" swatch; heatmap tiles no longer keep a selection highlight (they open the
  details on tap, as before). Transactions created by a recurrence get 00:00 instead of the original time of day.

### 7.5 Data access
- `RepositoryBase<T>` holding `EnsureInitializedAsync` + CRUD; `DataExportService` uses repositories.
- SQL aggregates in `BalanceService`: `SELECT AccountId, Type, SUM(AmountMinor) … GROUP BY` instead of loading all rows.
- Budget loading: one query for categories + one grouped spend query (no per-budget round trips).
- `IUnitOfWork` / `RunInTransactionAsync` helper used by the Stage 6 cascades.
- Done as `RepositoryBase<T>` (models implement `IEntity`): GetAll/GetById (`FindAsync`)/Save (insert when ID 0,
  else update)/Delete/DeleteById, each initializing the database first; `OnChanged` lets the cached Account and Category
  repositories drop their cache. All six repositories derive from it (−200 lines). `DatabaseInitializer` gained
  `GetConnectionAsync()` and `RunInTransactionAsync(work)`, used by AccountService/CategoryService (cascades),
  DataExportService (backup snapshot + restore) and the repositories. The connection is private (no public
  `Database` or `InitializeAsync` any more; the first `GetConnectionAsync` opens, migrates and seeds the database), and
  the unused `IDatabaseInitializer` / `IsInitialized` are gone. **SQL totals**: `TransactionRepository.SumCentsAsync` (period total by type, optional category),
  `SumCentsByCategoryAsync`, `SumCentsByAccountAsync`; `TransferRepository.SumCentsBySource/DestinationAsync` — all
  `SUM(ABS(AmountCents)) … GROUP BY` with `MAX(Date)` for last activity. `BalanceService.GetAllAccountBalancesAsync` runs
  four grouped queries instead of loading every transaction and transfer; `GetAccountBalanceAsync` reuses it.
  **Budgets**: `BalanceService.GetExpensesByCategoryAsync` (one grouped query) feeds `BudgetSummaryService` (it already
  loaded categories once since 7.3). The CSV export reads through the repositories. Removed (unused outside tests):
  `GetTotalBalanceAsync`, `GetBalanceAsync`, `GetLastTransactionDateForAccountAsync`, `GetBalancesByCurrencyAsync` (the
  Home card uses `CurrencyBalance.FromAccounts`, now tested directly) and the per-account/category list queries they used.
  Tests: `BalanceServiceTests` now run against a temp SQLite file (the sums are SQL), `RepositoryBaseTests`,
  `CurrencyBalanceTests` (265 pass).
- **Deviation:** no separate `IUnitOfWork` type — `DatabaseInitializer.RunInTransactionAsync` is that helper (the services
  already depended on `DatabaseInitializer`, and a sqlite-net transaction needs the raw connection anyway). The backup
  still reads the six tables directly inside one transaction rather than through the repositories, because it must be a
  point-in-time copy.

### 7.6 Async correctness
- Property-changed handlers stop calling `_ = LoadDataAsync()`; use a single `AsyncRelayCommand` with
  cancellation (`IncludeCancelCommand`) so a new period cancels the previous load.
- Debounce search text (300 ms) before filtering.
- Home "Recent" card (from the 7.3 review): in the account view it shows both halves of a transfer (2 of its 5 slots)
  and changes with the grouping mode. Build it from whole transfers, e.g. split transfers in Core after loading
  (`LedgerQueryService` returns whole transfers only) so the grouping toggle never reloads.
- `ErrorHandler`: log with `ILogger` (Serilog file sink in Stage 9); show friendly messages, not `ex.Message`.
- Done as: **Loads** — Home, Budget, the category reports and Trends each have one
  `[RelayCommand(AllowConcurrentExecutions = true)]` load taking a `CancellationToken`: the toolkit cancels the previous
  run's token when a new one starts, the load checks it after its awaits (sqlite-net takes no token) and drops its
  results, and only the last run clears the spinner. Period / month-count handlers call `LoadXCommand.Execute(null)`;
  `InitializeAsync`, deletes and edits await `ExecuteAsync`. Budget's initialize no longer loads the categories twice;
  Trends loads the categories with the transactions. The two non-load races got a "latest selection wins" guard: the
  transfer page's available balance (also used by the insufficient-funds check) and the expense page's budget warning.
  **Search** — Home's filter state moved to `TransactionFilterViewModel` (Core, MAUI-free, bound as `Filters.*`): typed
  text and the min/max amounts apply after 300 ms (`Helpers/Debouncer`), pickers at once, the search key and Clear at
  once and only once (Clear used to filter six times). **Recent card** — `LedgerService` (was `LedgerQueryService`)
  returns whole transfers only; `LedgerTransfers.PerAccount` splits them once per load for the account view
  (`LedgerEntry.TransferNote` keeps the transfer's own description for the halves' labels), so the Recent card shows one
  row per transfer in every view and the grouping toggle never reloads. `LedgerService.DeleteAsync` (transaction +
  receipt, or the whole transfer) moved out of `MainViewModel`; `CurrencyBalance.DisplayLines` formats the balance card.
  **Errors** — `ErrorHandler` logs the exception with `ILogger<ErrorHandler>` (Core references
  `Microsoft.Extensions.Logging.Abstractions`, MIT) and shows only the localized message; the 11 `FailedTo…: {0}` strings
  lost their `{0}` (no more `ex.Message` in dialogs, Trends showed a literal "{0}"), two new keys for account/category
  deletes, unused `FailedToLoadData`/`FailedToToggleView` removed. A cancellation is never shown. App/AppShell log
  through `ILogger` instead of `Debug.WriteLine`; `MainPage` lost its own try/catch. Tests: `DebouncerTests`,
  `LedgerTransfersTests`, `TransactionFilterViewModelTests` (first ViewModel tests), `LedgerServiceTests` (delete),
  `CurrencyBalanceTests`, `ErrorHandlerTests` (285 pass). Review: a cancellation reaching `ErrorHandler` is logged at Debug;
  `Debouncer`/`TransactionFilterViewModel` take a `TimeProvider` and their tests use `FakeTimeProvider`
  (`Microsoft.Extensions.TimeProvider.Testing`, MIT); only the latest debounced call runs; Budget runs its two queries
  together; `App` gets its service and logger by constructor injection.
- **Deviations:** (1) No `IncludeCancelCommand`: nothing in the UI cancels a load, and `AllowConcurrentExecutions`
  already cancels the previous run. (2) `MainViewModel` is 350 lines (was 455), not ~300: what is left is ~22
  one-line bindable properties, the load and the commands; Stage 8's Home redesign (bottom tabs) is the natural next split.
  (3) Release builds still log nowhere (only `AddDebug` in Debug) until the Serilog file sink (Stage 9).

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
