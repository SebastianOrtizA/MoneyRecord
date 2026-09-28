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

# Run tests (when test project exists)
dotnet test MoneyRecord.Tests/MoneyRecord.Tests.csproj
```

**Solution file**: `MoneyRecord.slnx` (XML format)

## Architecture

```
Models (SQLite entities)
  → Repositories (IRepository<T> + implementations)
    → Business Services (BalanceService, TransactionEnrichmentService)
      → ViewModels (CommunityToolkit.Mvvm, source generators)
        → Views (XAML pages + code-behind)
```

### Key Directories
- `Models/` — 5 SQLite tables: Account, Transaction, Category, Transfer, Budget. Plus DTOs (AccountBalanceInfo, CategoryReport, TransactionGroup, BudgetProgress)
- `Services/Interfaces/` — Repository and service interfaces (IRepository<T>, IAccountRepository, ICategoryRepository, ITransactionRepository, ITransferRepository, IBudgetRepository, IBalanceService, ITransactionEnrichmentService)
- `Services/Repositories/` — SQLite implementations + DatabaseInitializer
- `Services/` — Business services (BalanceService, TransactionEnrichmentService, CategoryIconService, NavigationService, LocalizationService, PreferencesService)
- `ViewModels/` — 10 ViewModels using [ObservableProperty] and [RelayCommand] source generators
- `Views/` — 9 XAML pages with code-behind
- `Controls/` — FloatingActionMenu (draggable FAB) + DecimalEntry (numeric keyboard)
- `Converters/` — 25+ value converters in single file
- `Helpers/` — BudgetProjectionHelper, DateRangeHelper, IconHelper, PeriodHelper
- `Resources/Strings/` — Localization: AppResources.resx (English), AppResources.es.resx (Spanish)

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
- Root routes: `//MainPage`, `//ExpenseReportPage`, `//IncomeReportPage`, `//BudgetPage`, `//TransfersPage`, `//ManageAccounts`, `//ManageIncomeCategories`, `//ManageExpenseCategories`
- Push routes: `AddTransactionPage`, `AddTransferPage`
- Custom `INavigationService` tracks navigation history for back button behavior

### Database
- SQLite via `sqlite-net-pcl` with `SQLiteAsyncConnection`
- DB file: `moneyrecord.db3` in `FileSystem.AppDataDirectory`
- No ORM relationships — joins done in application code via dictionary lookups
- `DatabaseInitializer` handles schema creation + seed data
- Seed data: 4 income categories, 7 expense categories, 1 "Cash" account

### Localization
- `.resx` resource files with `ResXFileCodeGenerator`
- XAML: `{ext:Localize KeyName}` markup extension (bindings, not static)
- Code: `AppResources.KeyName` or `LocalizationService.Instance.GetString(key)`
- Auto-detects system language (supports en, es)

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
- Currency formatting uses **device's CultureInfo** (no explicit currency selection yet)
- `BudgetPeriod` enum: Day, Month, Year — projected via `BudgetProjectionHelper`
- `PeriodType` enum for date range filtering: CalendarMonth, CalendarYear, Today, LastWeek, LastMonth, LastYear, CustomPeriod

## CodeGraph

This project is indexed by CodeGraph (`.codegraph/` directory). Use `codegraph_explore` as the primary tool for code exploration — it returns verbatim source, call paths, and blast radius in one call, including dynamic dispatch hops that grep can't follow.
