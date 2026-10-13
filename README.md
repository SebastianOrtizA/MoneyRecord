# MoneyRecord - Personal Finance Tracker

A .NET 10 MAUI app for tracking incomes, expenses and transfers across multiple accounts and currencies, with
budgets, recurring transactions, reports and full backups. Android is the primary target; iOS, macOS (Mac Catalyst)
and Windows are maintained too. All data stays on the device.

## Features

### Home
- **Balance card**: the total balance, with **one subtotal per currency** when accounts use different currencies
  (never added together). Tap it to see each account's balance and last activity.
- **Incomes / Expenses / Net** for the selected period. Negative amounts show the sign before the symbol (`-$50.00`) in red.
- **Periods**: Calendar Month (default), Calendar Year, Today, Last Week, Last Month, Last Year, Custom Period.
- **Transaction list**: grouped by category (default), by account, or a flat list; newest or oldest first; transfers
  appear alongside transactions. Groups are collapsible and show their total and transaction count.
- **Search and filters**: text search (description, category, account, tags), type, category, account and amount range.
- **Recent transactions** and a **budget summary**.
- **Pull to refresh**; quick income / transfer / expense buttons.

### Transactions
- Incomes and expenses with date, description, amount, account, category and optional **tags**.
- **Receipt photos** from the camera or gallery; unsaved photos are discarded when you cancel.
- Amount input formatted in the **account's currency** (symbol, separators, and no decimals for currencies like JPY).
- Optional **"Make recurring"** to create a recurring rule from the transaction.
- Edit and delete (with confirmation; deleting a transaction also deletes its receipt photo).

### Accounts
- Multiple accounts (cash, bank, savings, credit card, ...) with an icon, initial balance and **currency**.
- **Allow negative balance** per account (credit cards, debts). The seeded "Cash" account allows it.
- **Safe delete**: transactions and recurring rules move to the default account in one database transaction. Deleting
  is refused when the account has transfers (delete those first) or when the default account uses another currency.

### Transfers
- Move money between accounts, with date, amount and description; a warning is shown when the accounts' currencies differ.
- Transfer history with edit and delete.

### Categories
- Separate income and expense categories with Material Design icons.
- Seeded on first run: Salary, Freelance, Investment, Other Income; Food, Transportation, Entertainment, Utilities,
  Shopping, Home, Other Expense.
- **Safe delete**: transactions, recurring rules and the budget move to a replacement category (the budget is deleted
  if the replacement already has one), after a confirmation that says what will move.

### Budgets
- Spending limits per **expense** category, per day, month or year, projected to the selected period
  (a daily budget of $10 shows as $310 for a 31-day month).
- Progress bars with light/dark-aware colours: green under 60%, amber 60–80%, orange 80–100%, red when exceeded.
- Totals: budgeted, spent, remaining.

### Recurring transactions
- Daily, weekly, biweekly, monthly (optionally on a specific day) or yearly rules, with an optional end date.
- Due transactions are created when the app starts.

### Reports
- **Expense / Income reports**: heatmaps by category, small categories grouped under "Others"; tap a category for its
  transactions (with one subtotal per currency).
- **Trends**: monthly spending, income vs. expenses and expense breakdown over the last 3, 6 or 12 months.

### Settings, backup & restore
- **Base currency** (default: the device region's), used for totals that span accounts.
- **Full backup** as a `.zip` (`data.json` + receipt photos), saved locally or shared.
- **Restore** from a local or picked backup (also older `.json` backups). The restore is all-or-nothing; the current data
  is saved to an automatic snapshot first (the last 5 are kept) and the "Restore complete" dialog offers **Undo**.
- **CSV export** of transactions.

### Localization
- English and Spanish, picked from the device language; the device region is kept for currency and number formats
  (e.g. `es-CO`).
- Dates use the culture's own patterns ("October 11, 2026" / "11 de octubre de 2026"), and counts use proper plurals.

### Navigation
- Shell flyout menu with an icon per page; Android back button returns to Home, and closes the app from Home.
- **Floating action menu**: a draggable button (position remembered, snaps to the nearest edge) with quick
  income / transfer / expense actions.

### Theming
- Full light and dark mode.

## Data & storage
- SQLite (`moneyrecord.db3` in the app's data folder) via `sqlite-net-pcl`.
- **Money is stored as integer cents** (`AmountCents`, ...), so totals never drift.
- **Schema migrations**: `PRAGMA user_version` plus a migration runner; each migration runs in its own transaction, and
  migrations that rewrite data back up the database first (`moneyrecord.db3.bak-v{n}`).

### Known limitation
Totals that span accounts in different currencies (Home income/expense/net, reports, budgets, charts) are still added
together and shown in the base currency. Proper conversion needs exchange rates (planned).

## Build, run & test

Requires the .NET 10 SDK with the MAUI workloads (`dotnet workload install maui`).

```bash
# Android (primary target)
dotnet build MoneyRecord/MoneyRecord.csproj -f net10.0-android
dotnet build MoneyRecord/MoneyRecord.csproj -t:Run -f net10.0-android   # deploy to a running emulator/device

# Windows
dotnet build MoneyRecord/MoneyRecord.csproj -f net10.0-windows10.0.19041.0

# Unit tests (xUnit; plain net10.0, runs on Windows, Linux and macOS)
dotnet test MoneyRecord.Tests/MoneyRecord.Tests.csproj
```

## Technology stack
- **.NET 10 / .NET MAUI** (Android, iOS, Mac Catalyst, Windows)
- **CommunityToolkit.Mvvm** (MVVM source generators) and **CommunityToolkit.Maui** (toasts, behaviours)
- **sqlite-net-pcl** + **SQLitePCLRaw** (local database)
- **Material Design Icons** font
- Tests: **xUnit**, **NSubstitute**, **AwesomeAssertions**

## Architecture

```
Models (SQLite entities, money in cents)
  → Repositories (IRepository<T>, SQLite, migrations)
    → Services (balances, enrichment, accounts/categories, currency, recurring, backup)
      → ViewModels (CommunityToolkit.Mvvm)
        → Views (XAML pages)
```

## Project structure

```
MoneyRecord.Core/       Plain net10.0 library, no MAUI (same namespaces as the app)
├── Helpers/            Money (cents), MoneyFormatter, AmountParser, DateFormatter, Plurals, BudgetProjectionHelper,
│                       DateRangeHelper, PeriodHelper, IconHelper, ReceiptFiles, ReceiptDraft, LedgerFilter, LedgerGrouping,
│                       LedgerTransfers, CategoryReportBuilder, RecurrenceCalculator, Debouncer
├── Models/             Account, Transaction, Transfer, Category, Budget, RecurringTransaction (tables)
│                       + display models (LedgerEntry, TransactionGroup, CategoryAmount, AccountBalanceInfo, BudgetProgress, ...)
├── Resources/Strings/  AppResources.resx (English), AppResources.es.resx (Spanish)
├── Services/
│   ├── Interfaces/     Service and repository interfaces, IAppPaths
│   ├── Repositories/   RepositoryBase<T> + SQLite repositories (SQL totals), DatabaseInitializer (schema + seed data)
│   │   └── Migrations/ IMigration, MigrationRunner, M001_Baseline, M002_MoneyAsCents, M003_AccountCurrencyCodes
│   ├── AccountService, CategoryService          Transactional deletes
│   ├── BalanceService                           Account balances and period totals (SQL sums)
│   ├── TransactionEnrichmentService             Names/icons/currencies for display
│   ├── LedgerService                            Home list rows (transactions + whole transfers), delete one
│   ├── BudgetSummaryService                     Budget progress for a period (Home + Budget page)
│   ├── CurrencyService                          Currencies, base currency, decimals
│   ├── RecurringTransactionService              Creates due recurring transactions
│   ├── DataExportService                        Zip backup, restore + snapshot, CSV export
│   ├── ErrorHandler                             Friendly message to the user, exception to ILogger
│   └── AppPaths, LocalizationService, CategoryIconService
└── ViewModels/         TransactionFilterViewModel (Home search + filter panel, debounced typing)
MoneyRecord/            The MAUI app
├── Behaviors/          CurrencyMaskBehavior (amount input in the account's currency)
├── Controls/           FloatingActionMenu (+ FabComponents/), DecimalEntry, ChartDrawables
├── Converters/         One XAML value converter per file (money, dates, colours, heatmap, ...)
├── Extensions/         LocalizeExtension ({ext:Localize Key} in XAML)
├── Platforms/          Android (back button, numeric entry handler), iOS, MacCatalyst, Windows
├── Services/           NavigationService, DialogService, PreferencesService
├── ViewModels/         One per page + FloatingMenuViewModel
├── Views/              Main, AddTransaction, AddTransfer, Transfers, ManageAccounts, ManageCategories, Budget,
│                       RecurringTransactions, CategoryReport (Expense/Income), TrendsReport, Settings
├── App.xaml            Converters and styles; processes recurring transactions on start
├── AppShell.xaml       Flyout menu and routes
└── MauiProgram.cs      Dependency injection
MoneyRecord.Tests/      xUnit tests (net10.0, Core only): helpers, models, services, view models, migrations, backup/restore,
                        resource parity
```

## License

MoneyRecord is licensed under the **GNU Affero General Public License v3.0** (see [LICENSE](LICENSE)).

Third-party components:
- .NET MAUI, CommunityToolkit.Mvvm, CommunityToolkit.Maui: MIT
- sqlite-net-pcl: MIT; SQLitePCLRaw: Apache-2.0; SQLite: public domain
- LiveCharts2: MIT; SkiaSharp / HarfBuzzSharp: MIT (with the Skia, HarfBuzz, FreeType, ICU, … notices they bundle)
- Material Design Icons: Apache-2.0 (font and icons, Pictogrammers Free License); Open Sans: Apache-2.0
- Test-only: xUnit (Apache-2.0), NSubstitute (BSD-3-Clause), AwesomeAssertions (Apache-2.0)

The app lists the shipped ones with their full licence texts under Settings → About
(`MoneyRecord/Resources/Raw/Licenses/`).
