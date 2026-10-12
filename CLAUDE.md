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
  (AccountBalanceInfo, CurrencyBalance, LedgerEntry, TransactionGroup, TransactionFilter/FilterOption,
  CategoryAmount/CategoryReport (report shares + heat level), BudgetProgress/BudgetLevel, TypeFilterOption, Currency)
- `Services/Interfaces/` — Repository and service interfaces (`IRepository<T>`, one per repository and service), `IAppPaths`
- `Services/Repositories/` — `RepositoryBase<T>` (CRUD over `IEntity`; cached Account/Category repositories override
  `OnChanged`) + one repository per table, `DatabaseInitializer` (schema, seed data, runs migrations;
  `GetConnectionAsync`, `RunInTransactionAsync`)
- `Services/Repositories/Migrations/` — `IMigration`, `MigrationRunner` (`All` = ordered list), `M001`–`M003`
- `Services/` — BalanceService, TransactionEnrichmentService, LedgerQueryService (Home rows: transactions + transfers
  as `LedgerEntry`), BudgetSummaryService (budget progress for a period: Home card + Budget page),
  AccountService / CategoryService (transactional deletes),
  CurrencyService, RecurringTransactionService, DataExportService (zip backup/restore, CSV), CategoryIconService,
  LocalizationService, AppPaths (app data folder → database, receipts, backups), ErrorHandler (via `IDialogService`),
  NavigationHistory (back-button stack; `//Route` = root)
- `Helpers/` — Money, MoneyFormatter, AmountParser, DateFormatter, Plurals, BudgetProjectionHelper, DateRangeHelper,
  IconHelper (only place that parses icon hex codes), PeriodHelper, ReceiptFiles, ReceiptDraft, LedgerFilter (search/filter/sort
  by IDs), LedgerGrouping, CategoryReportBuilder, RecurrenceCalculator (occurrences at 00:00; `DateRangeHelper.GetBudgetWindow`
  for a budget's current day/month/year)
- `Resources/Strings/` — Localization: AppResources.resx (English), AppResources.es.resx (Spanish); public class

`MoneyRecord/` (app)
- `ViewModels/` — 12 ViewModels using [ObservableProperty] and [RelayCommand] source generators
- `Views/` — 11 XAML pages with code-behind; `CategoryReportPage` serves both reports (`ExpenseReportPage` /
  `IncomeReportPage` are code-only subclasses that set the type)
- `Controls/` — FloatingActionMenu (draggable FAB, `FabComponents/`), DecimalEntry (numeric keyboard), ChartDrawables (Trends)
- `Behaviors/` — CurrencyMaskBehavior (amount input in the account's currency)
- `Converters/` — one value converter per file (registered in `App.xaml`); InvertedBool, IntToBool and
  IsStringNotNullOrEmpty come from CommunityToolkit.Maui (exceptions suppressed in `MauiProgram`)
- `Services/` — NavigationService, DialogService (Shell alerts/prompts/action sheets + toast), PreferencesService
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
- Views set `BindingContext` in constructor via DI, call `ViewModel.InitializeAsync()` in `OnAppearing()`

### Dependency Injection (MauiProgram.cs)
- Services and repositories: **Singleton**
- Pages and ViewModels: **Transient** (except MainPage + MainViewModel = Singleton)
- All registrations use interface → implementation mapping

### Navigation
- Shell with Flyout menu (`AppShell.xaml`)
- Root routes: `//MainPage`, `//ExpenseReportPage`, `//IncomeReportPage`, `//TrendsReportPage`, `//BudgetPage`, `//RecurringTransactionsPage`, `//TransfersPage`, `//ManageAccounts`, `//ManageIncomeCategories`, `//ManageExpenseCategories`, `//SettingsPage`
- Push routes: `AddTransactionPage`, `AddTransferPage`
- Custom `INavigationService` tracks navigation history for back button behavior: AppShell's `Navigated` event feeds
  `NavigationHistory`; Android back → `Shell.SendBackButtonPressed()` → `AppShell.OnBackButtonPressed` (previous page,
  then Home, then close)

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
