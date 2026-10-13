# MoneyRecord

## Project Overview

.NET 10 MAUI personal finance app for tracking incomes, expenses, transfers, budgets, and accounts. Android is the primary target (APK), but all platform targets (iOS, macCatalyst, Windows) are maintained.

**License**: AGPL-3.0

## Build & Run

```bash
# Build Android
dotnet build MoneyRecord/MoneyRecord.csproj -f net10.0-android

# Build Windows
dotnet build MoneyRecord/MoneyRecord.csproj -f net10.0-windows10.0.19041.0

# Run on Android emulator/device
dotnet build MoneyRecord/MoneyRecord.csproj -t:Run -f net10.0-android

# Run tests (xUnit + NSubstitute + AwesomeAssertions; plain net10.0, run from PowerShell)
dotnet test MoneyRecord.Tests/MoneyRecord.Tests.csproj
```

On the emulator, use the project skill `/run-moneyrecord` (boot, deploy, screenshots, locale switching).
Plans for the staged roadmap live in `.claude/plans/` (one file per stage).

**Solution file**: `MoneyRecord.slnx` (XML format)

## Architecture

```
Models (SQLite entities, money in cents)
  → Repositories (IRepository<T> + implementations, migrations)
    → Business Services (BalanceService, TransactionEnrichmentService, AccountService, CategoryService, ...)
      → ViewModels (CommunityToolkit.Mvvm, source generators)
        → Views (XAML pages + code-behind)
```

### Key Directories
Two projects: `MoneyRecord.Core/` (plain `net10.0`, no MAUI) and `MoneyRecord/` (the MAUI app). Namespaces are the same
in both (`MoneyRecord.Models`, `MoneyRecord.Helpers`, ...). XAML that uses a Core type needs
`xmlns:models="clr-namespace:MoneyRecord.Models;assembly=MoneyRecord.Core"` — without `assembly=` a Debug build compiles but
crashes at runtime (XAML is inflated at runtime there).

`MoneyRecord.Core/`
- `Models/` — 6 SQLite tables: Account, Transaction, Category, Transfer, Budget, RecurringTransaction. Plus display models
  (AccountBalanceInfo, AccountGroup/AccountListItem (Accounts screen: per-currency groups), CurrencyBalance,
  LedgerEntry, TransactionGroup, TransactionFilter/FilterOption, CategoryAmount/CategoryReport (report shares + chart
  palette slot, donut slices), PeriodDelta ("vs previous period"), BudgetProgress/BudgetLevel, TypeFilterOption,
  Currency, DefaultCategory (the seed categories by resource key; also offered by the setup))
- `Services/Interfaces/` — Repository and service interfaces (`IRepository<T>`, one per repository and service), `IAppPaths`
- `Services/Repositories/` — `RepositoryBase<T>` (CRUD over `IEntity`; cached Account/Category repositories override
  `OnChanged`) + one repository per table, `DatabaseInitializer` (schema, seed data, runs migrations;
  `GetConnectionAsync`, `RunInTransactionAsync`)
- `Services/Repositories/Migrations/` — `IMigration`, `MigrationRunner` (`All` = ordered list), `M001`–`M003`
- `Services/` — BalanceService, TransactionEnrichmentService, LedgerService (Home rows: transactions + whole transfers
  as `LedgerEntry`; deletes one), BudgetSummaryService (budget progress for a period: Home card + Budget page),
  AccountService / CategoryService (transactional deletes), EntryDefaultsService (add form: last account and recent
  categories per type, in `IPreferencesService`), CurrencyService, RecurringTransactionService, DataExportService
  (zip backup/restore, CSV), CategoryIconService, SetupService (first-run setup: pending only on a new install;
  applies the chosen accounts/categories, replacing the seed data on the first run),
  LocalizationService (saved `Language` preference, empty = device), AppPaths (app data folder → database, receipts,
  backups), ErrorHandler (localized message via
  `IDialogService`, exception to `ILogger`; never show `ex.Message`),
  NavigationHistory (back-button stack; `//Route` = root), TransactionsDrillDown (a report asks Transactions to open
  filtered; taken once)
- `Helpers/` — Money, MoneyFormatter, AmountParser, DateFormatter, Plurals, BudgetProjectionHelper, DateRangeHelper,
  IconHelper (only place that parses icon hex codes), PeriodHelper, PickList (unique action-sheet labels), ReceiptFiles,
  ReceiptDraft, LedgerFilter (search/filter/sort by IDs), LedgerGrouping, LedgerTransfers (splits transfers for the
  account view), Debouncer, CategoryReportBuilder, EntryDescription (empty → category name), RecurrenceCalculator
  (occurrences at 00:00; `DateRangeHelper.GetBudgetWindow` for a budget's current day/month/year)
- `Resources/Strings/` — Localization: AppResources.resx (English), AppResources.es.resx (Spanish); public class
- `ViewModels/` — MAUI-free view models (testable): TransactionFilterViewModel (Transactions tab search + filter chips;
  `ShowOnly` for a report drill-down), PeriodViewModel (singleton: the one period Home, Transactions, Budgets and
  Spending / Income show, plus the shared month count of the time-series reports), SetupViewModel (setup assistant:
  language + base currency → starting accounts → default categories)

`MoneyRecord/` (app)
- `ViewModels/` — 14 ViewModels using [ObservableProperty] and [RelayCommand] source generators
- `Views/` — 14 XAML pages with code-behind. Home keeps the summary cards; the full list is on `TransactionsPage`
  (one CollectionView of entries or groups via `LedgerItemTemplateSelector`; swipe edits/deletes, a tap opens details).
  `CategoryReportPage` serves both reports (`ExpenseReportPage` / `IncomeReportPage` are code-only subclasses that set
  the type). `MorePage` lists the screens without a tab. `SetupPage` is the window's page on a new install
  (swapped for `AppShell` when done) and a pushed route from Settings
- `Charts/` — ChartPalette (light/dark chart colours, categorical slots), ChartFactory (LiveChartsCore donut / columns /
  lines / axes with one look; per-series tap handlers), ChartTapBehavior (drill-down on a real tap, never on a scroll)
- `Controls/` — BottomSheet (the in-page sheet: dim, panel, handle, slide-up; `IsOpen` + `CloseCommand`), PeriodPicker /
  MonthCountPicker (bind BindingContext to a page's `Period`), AddEntryButton (the one add button: opens an Expense /
  Income / Transfer sheet; on Home and Transactions, named `AddEntry`), IOverlayHost (a page with a sheet: Android back
  closes it first),
  LedgerItemTemplateSelector, FilterChip (`IsActive` → active colours via its style), DecimalEntry (numeric keyboard)
- `Behaviors/` — CurrencyMaskBehavior (amount input in the account's currency)
- `Converters/` — one value converter per file (registered in `App.xaml`); InvertedBool, IntToBool and
  IsStringNotNullOrEmpty come from CommunityToolkit.Maui (exceptions suppressed in `MauiProgram`)
- `Services/` — NavigationService, DialogService (alerts/prompts/action sheets on the window's page + toast), PreferencesService
  (MAUI Preferences). ViewModels use `IDialogService` / `INavigationService`, never `Shell.Current` or `Toast`
- `MauiProgram.cs` — DI; `IAppPaths` = `new AppPaths(FileSystem.AppDataDirectory)`

`MoneyRecord.Tests/` — xUnit tests on plain `net10.0`, referencing Core only (helpers, models, services, migrations
against real temp SQLite files, backup/restore); they run on Linux too (CI uses ubuntu-latest)

## Patterns & Conventions

### MVVM
- ViewModels extend `ObservableObject` (CommunityToolkit.Mvvm)
- Properties: `[ObservableProperty]` source generator (generates `PropertyName` from `propertyName` field)
- Commands: `[RelayCommand]` source generator (generates `XxxCommand` from `XxxAsync` method)
- Partial methods: `partial void OnPropertyNameChanged(T value)` for property change reactions
- Reloads: one `[RelayCommand(AllowConcurrentExecutions = true)]` load taking a `CancellationToken`; a new run cancels
  the previous one (check the token after the awaits, catch the cancellation, only the last run clears the spinner).
  Property-changed handlers call `LoadXCommand.Execute(null)`, never `_ = LoadXAsync()`
- Views set `BindingContext` in constructor via DI, call `ViewModel.InitializeAsync()` in `OnAppearing()`

### Dependency Injection (MauiProgram.cs)
- Services and repositories: **Singleton**
- Pages and ViewModels: **Transient** (except MainPage + MainViewModel = Singleton)
- All registrations use interface → implementation mapping

### Navigation
- Shell `TabBar` (`AppShell.xaml`): Home · Transactions · Reports (Spending / Income / Trends / Cash flow / Net worth
  top tabs) · Budgets · More. Tabs keep implicit routes, so each tab page is a root at `//MainPage`,
  `//TransactionsPage`, `//ExpenseReportPage`, `//IncomeReportPage`, `//TrendsReportPage`, `//CashFlowReportPage`,
  `//NetWorthReportPage`, `//BudgetPage`, `//MorePage`
- Push routes: `AddTransactionPage`, `AddTransferPage` (tab bar hidden) and the More screens `ManageAccountsPage`,
  `ManageCategoriesPage` (Income / Expense switch), `RecurringTransactionsPage`, `TransfersPage`, `SettingsPage`
- Home links: balance card → `ManageAccountsPage` (`?CurrencyCode=` from a currency line filters it), Recent "See
  all" → `//TransactionsPage`, budgets "See all" → `//BudgetPage`
- Custom `INavigationService` tracks navigation history for back button behavior: AppShell's `Navigated` event feeds
  `NavigationHistory`; Android back → `Shell.SendBackButtonPressed()` → `AppShell.OnBackButtonPressed` (an open sheet
  on an `IOverlayHost` page, then the previous page, then Home, then close)

### Database
- SQLite via `sqlite-net-pcl` with `SQLiteAsyncConnection`
- DB file: `moneyrecord.db3` in `FileSystem.AppDataDirectory`
- No ORM relationships — joins done in application code via dictionary lookups
- `DatabaseInitializer` handles schema creation + seed data, then the migration runner
- Seed data: 4 income categories, 7 expense categories, 1 "Cash" account (allows negative balance, device currency)
- **Money is stored as integer cents** (`AmountCents`, `InitialBalanceCents`, `LimitAmountCents`); the `decimal` properties
  are `[Ignore]` wrappers (`Helpers/Money`). Fixed scale of 100 for every currency; zero-decimal currencies (JPY, ...)
  only change display/input (`CurrencyService.GetMinorUnits`)
- **Migrations** (`PRAGMA user_version`): the models describe the latest schema. A new database is created from the models
  and stamped with the latest version; a migration only transforms a database from an older app version (after
  `CreateTableAsync` has added new columns/indexes). Add new migrations at the end of `MigrationRunner.All`; set
  `RewritesData = true` to get a `moneyrecord.db3.bak-v{n}` backup first. Indexes are `[Indexed(Name = "IX_…")]` on the models
- Multi-row changes go through `DatabaseInitializer.RunInTransactionAsync` (deletes in AccountService/CategoryService,
  backup snapshot, restore)
- Totals are summed by SQLite (`SUM(ABS(AmountCents)) … GROUP BY` in the repositories), never by loading every row
- Backups: `.zip` with `data.json` (format v3, rows keep their IDs) + `receipts/`; restore snapshots go to `Backups/`

### Localization
- `.resx` resource files with `ResXFileCodeGenerator`
- XAML: `{ext:Localize KeyName}` markup extension (bindings, not static)
- Code: `AppResources.KeyName` or `LocalizationService.Instance.GetString(key)`
- Auto-detects system language (supports en, es)
- `LocalizationService.ResolveCulture` keeps the device region (`es` + `CO` → `es-CO`), never a neutral culture
- Dates: `DateFormatter` / `DateTextConverter` (`Long`, `Medium`, `MonthDay`), never fixed patterns like `"MMM dd, yyyy"`
- Counts: `Plurals` (`...One` / `...Other` keys), not `"{0} items"`
- Filters and logic compare enums/IDs, never display strings
- `ResourceParityTests` fail when a key, translation or `{n}` placeholder is missing in either language

### Icons
- Material Design Icons font (`materialdesignicons-webfont.ttf`, alias "MaterialDesignIcons")
- Hex codes stored as strings (e.g., `"F0770"` for tag icon)
- `IconHelper.GetDisplayIcon()` converts hex to Unicode character
- Default category icon: `"F0770"`, default account icon: `"F0070"`

### Theming
- Full light/dark support via `AppThemeBinding` throughout XAML
- Colors defined in `Resources/Styles/Colors.xaml`
- Primary: `#512BD4` (purple)

## Key Decisions

- Budget is **expense-only** (per-category spending limits, not income targets)
- Transfers are a **separate table** from transactions (linked via TransferId on display)
- Each account has a **currency**; amounts are shown with `MoneyFormatter.Format(amount, currencyCode)` / `MoneyConverter`
  / `CurrencyMoneyConverter`. An empty code means the **base currency** (Settings; default = device region currency)
- Never add different currencies together: the Home card shows one subtotal per currency. Totals across accounts in
  reports/budgets are still summed in the base currency until exchange rates exist (Stage 10.3)
- Accounts with transfers can't be deleted; account deletes into a different currency are refused
- Budget progress uses `BudgetLevel` + `BudgetLevelToColorConverter` (light/dark aware); models hold no `Color`
- `BudgetPeriod` enum: Day, Month, Year — projected via `BudgetProjectionHelper`
- `PeriodType` enum for date range filtering: CalendarMonth, CalendarYear, Today, LastWeek, LastMonth, LastYear, CustomPeriod

## CodeGraph

This project is indexed by CodeGraph (`.codegraph/` directory). Use `codegraph_explore` as the primary tool for code exploration — it returns verbatim source, call paths, and blast radius in one call, including dynamic dispatch hops that grep can't follow.
