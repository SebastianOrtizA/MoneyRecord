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

# Release builds use R8 (obfuscation, mapping.txt next to the APK/AAB; CI hands it to Play). Keep rules: MoneyRecord/Platforms/Android/proguard.cfg
# (a Java class used by name that R8 renames or removes only fails at runtime: smoke-test a Release APK after adding Android libraries)

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
- `Models/` — 16 SQLite tables: SavingsChallenge + SavingsChallengeDay (savings game: `ChallengePlan.Build` = day amounts, doubling in levels or linear, optional rounding; days done keep their amount; optional destination account or pocket, each day asks the source account or pocket and moves the money (transfer / take-out / set-aside); `ISavingsChallengeService`; `ChallengeSummary` displays it), Account, Transaction, Category, Transfer, Budget, RecurringTransaction, SavingsGoal +
  SavingsGoalAccount (a goal's accounts; one goal per account) + SavingsGoalPocket (its pockets), Pocket + PocketMovement (money set aside inside an account, an earmark only: signed movements, balance = their sum; `PocketSummary` displays one), Payee (optional on a transaction and a recurring one;
  `PayeeSummary` = with use count + last use), ExchangeRate (downloaded daily rates; a cache, not in backups), Trip + TripCategory + TripBudget (a trip, its shared trip categories, its per-category limits; `Transaction.TripId`/`TripCategoryId`, `Category.IsTravel` = the system "Travel" category trip expenses sit in while the trip is active; `TripCard`/`TripSummary` display it), Debt (money lent / borrowed: a payee + a hidden account, `Account.IsDebt`,
  whose transfers are the debt's money; `DebtSummary` / `DebtMovement` display it).
  `Transaction.SplitId`: the parts of a split expense are plain transactions sharing the ID (`SplitPart` = one part to save). Plus display models
  (AccountBalanceInfo, AccountGroup/AccountListItem (Accounts screen: per-currency groups), CurrencyBalance,
  CurrencyAmount (a total in one currency; per-currency totals, never added), PeriodTotals (Home: income / expense / net
  per currency), GoalProgress (a goal's saved / status / plan texts), CurrencyChoice (report currency chip; the first one "≈ USD" = converted), BudgetSpent (counted vs
  listed spending),
  LedgerEntry, TransactionGroup, TransactionFilter/FilterOption, CategoryAmount/CategoryReport (report shares + chart
  palette slot, donut slices), PeriodDelta ("vs previous period"), BudgetProgress/BudgetLevel, TypeFilterOption,
  Currency, DefaultCategory (the seed categories by resource key; also offered by the setup), LanguageOption /
  ThemeOption / BillReminderOption / LockDelayOption (picker items, named in the current language), AppNotification, UpcomingEntry (a scheduled recurring occurrence:
  Home card + Recurring page), ThirdPartyComponent (About page licence list; a test
  checks it covers every package reference))
- `Services/Interfaces/` — Repository and service interfaces (`IRepository<T>`, one per repository and service), `IAppPaths`
- `Services/Repositories/` — `RepositoryBase<T>` (CRUD over `IEntity`; cached Account/Category repositories override
  `OnChanged`) + one repository per table, `DatabaseInitializer` (schema, seed data, runs migrations;
  `GetConnectionAsync`, `RunInTransactionAsync`; optional encryption), `SqliteSetup`
- `Services/Repositories/Migrations/` — `IMigration`, `MigrationRunner` (`All` = ordered list), `M001`–`M004`
- `Models/HomeCard` + `HomeLayout` (Home cards' order + hidden set, one preference string; Balance never hidden; new cards appended) + `HomeLayoutSettings`; Settings → Home screen (`HomeLayoutPage`, move arrows + switches) and Home's "Customize Home" link; a hidden card skips its loads in `MainViewModel`, `MainPage.ArrangeCards` reorders the cards; the opt-in Debts card (off by default, `DebtSummary.OutstandingTotals`) lists the open debts
- `Services/` — BalanceService, TransactionEnrichmentService, LedgerService (Home rows: transactions + whole transfers
  as `LedgerEntry`; deletes one), BudgetSummaryService (budget progress for a period: Home card + Budget page), PayeeService (found or created by name on save,
  last category per type, rename / merge / delete), DebtService + DebtLedger (debts: totals in SQL, payments with an interest part,
  the schedule = a `RecurringTransaction.DebtId` rule capped at what is left; `IAccountRepository.GetOwnAsync` leaves the
  hidden accounts out of pickers and lists), SplitService (saves a split expense: one transaction per part in one DB
  transaction, replacing the edited ones; the own conversion shared out), PocketService (set aside / take out, available = balance minus pockets, `GetShortfallAsync`, `CoverAsync`; `PocketCover.Plan`; `PocketCoverPrompt` = the question the expense and transfer forms ask before saving an entry that leaves its account with less than its pockets hold: take it from which pocket, keep, or cancel; the take-outs are recorded after the save, linked to the entry; recurring, CSV import, debt payments and party shares never ask), SavingsGoalService (goal
  progress = the linked accounts' balance in the goal's currency; `GoalProjection`: needed per month, 3-month pace),
  PartyService (friends list, parties with members, expenses split evenly minus exclusions, contributions to a common pot;
  summary = balances + settle-up payments), TripService (one active trip; summary converted into the trip currency; End maps each trip category to a real category, Reopen puts expenses back in Travel; trip category CRUD),
  AccountService / CategoryService (transactional deletes), EntryDefaultsService (add form: last account and recent
  categories per type, in `IPreferencesService`), CurrencyService, RecurringTransactionService (a variable-amount schedule, `IsVariableAmount`, is never created by processing: each due date is a pending `UpcomingEntry` to enter via the add form's `RecurringId` + `Occurrence` route or skip; `LastAmountCents` = the estimate), DataExportService
  (zip backup/restore, CSV), CategoryIconService, SetupService (first-run setup: pending only on a new install;
  applies the chosen accounts/categories, replacing the seed data on the first run),
  WhatsNewTracker + `IWhatsNewService` (the "What's new" alert: the Play changelog `fastlane/metadata/android/*/changelogs/default.txt`, bundled as `Changelog/{locale}.txt` assets and picked by `WhatsNewChangelog.Locale`, shown once per version after an update, again from About; a new install skips it),
  LocalizationService (saved `Language` preference, empty = device), AppPaths (app data folder → database, receipts,
  backups), ErrorHandler (localized message via
  `IDialogService`, exception to `ILogger`; never show `ex.Message`),
  NavigationHistory (back-button stack; `//Route` = root), TransactionsDrillDown (a report asks Transactions to open
  filtered; taken once), ReminderService (daily job: bill reminders, recurring processing, budget alerts; the one-time
  offer) + ReminderSettings + `INotificationService`, NotificationLaunch (a tapped notification opens its page),
  AppLock (when the app lock locks: start, back after the delay; never without a phone lock) + PrivacySettings +
  `IDeviceLock`, ExchangeRateService (opt-in Frankfurter download of the missing days, at start and in the daily
  job) + ExchangeRateSettings + `IExchangeRateSource` = FrankfurterRateSource, ConversionService (the "Show in" switch
  applied: converted or by-currency totals for Home, reports and budgets) + CurrencyDisplaySettings,
  ReportExportService (Reports → Export: a `PeriodReport` for a `ReportRequest`, written as a PDF by
  `Pdf/PeriodReportPdf` (MigraDoc 6.2.4, the last MIT version); `Pdf/PdfFonts` = the embedded Open Sans and `Printable`
  for what it lacks), `IFileSharer` (cache file + share sheet; MAUI `FileSharer`),
  StatementImportService (CSV import into one account: preview with duplicates + learned categories, import, undo;
  `StatementMapping` = the columns, `ImportRow` = a preview row), StatementMappingStore (the last import's
  columns + account per header layout, in preferences), AutoBackupService (scheduled backups into the picked folder:
  due by whole days, skipped while the data fingerprint is unchanged, keeps the newest N) + AutoBackupSettings +
  `IBackupFolder`, `IBackupPasswordStore` (the backup password; `BackupPasswordException` when a backup needs it), BalanceWidget
  (the home-screen widget: account per widget ID, hidden like Home, add-form routes) + `IHomeScreenWidgets` (redraw)
- `Helpers/` — Money, MoneyFormatter, AmountParser, DateFormatter, Plurals, BudgetProjectionHelper, DateRangeHelper,
  IconHelper (only place that parses icon hex codes), PeriodHelper, PickList (unique action-sheet labels), ReceiptFiles,
  ReceiptDraft, LedgerFilter (search/filter/sort by IDs), LedgerGrouping, LedgerTransfers (splits transfers for the
  account view), Debouncer, CategoryReportBuilder, EntryDescription (empty → category name), PayeeNames (normalize, same name ignoring case, suggestions), SplitAllocation (left to assign; a total shared in proportion), RecurringStart (a new schedule: first entry now, or only the schedule for a future date), RecurrenceCalculator
  (occurrences at 00:00; `DateRangeHelper.GetBudgetWindow` for a budget's current day/month/year), UpcomingSchedule (next 30
  days of recurring occurrences, same rule), TransferRate (the rate a cross-currency transfer implies), ReportCurrencies
  (currencies in use; transactions in one currency), PartyLedger (a party's even split with rotating leftover cents, balances with the pot as line ID 0, settle-up: largest debtor pays largest creditor), CurrencyConverter (downloaded rates: nearest earlier day,
  inverse, across an earlier base; a transaction's own "Amount in USD" first), CsvTable / StatementDate /
  StatementAmount (bank statement CSV: table, date layouts, signed amounts), ReceiptReader (a receipt's
  OCR lines → rows → total + date), ReceiptFill (what a reading changes in the add form)
- `Resources/Strings/` — Localization: AppResources.resx (English), AppResources.es.resx (Spanish); public class
- `ViewModels/` — MAUI-free view models (testable): TransactionFilterViewModel (Transactions tab search + filter chips;
  `ShowOnly` for a report drill-down), PeriodViewModel (singleton: the one period Home, Transactions, Budgets and
  Spending / Income show, plus the shared month count of the time-series reports and the reports' currency, `ReportCurrency`), SetupViewModel (setup assistant:
  language + base currency → starting accounts → default categories), ReportExportViewModel (the Reports tabs' Export button: PDF or period CSV), AutoBackupViewModel (Settings → Automatic backups), BackupPasswordViewModel (backup
  password switch; restore asks for a protected backup's password)

`MoneyRecord/` (app)
- `ViewModels/` — 19 ViewModels using [ObservableProperty] and [RelayCommand] source generators
- `Views/` — 23 XAML pages (`ChallengesPage` = More → Savings challenge: cards + add/edit BottomSheet with a live plan preview; `ChallengePage` = the game: 7-column grid, a tap marks a day, optional transfer prompt) with code-behind. A row's `LedgerEntry.Title` is the payee when the description is automatic. Home keeps the summary cards; the full list is on `TransactionsPage`
  (one CollectionView of entries or groups via `LedgerItemTemplateSelector`; swipe edits/deletes, a tap opens details).
  `CategoryReportPage` serves both reports (`ExpenseReportPage` / `IncomeReportPage` are code-only subclasses that set
  the type). `MorePage` lists the screens without a tab. `AboutPage` / `LicensePage` show the licences (full texts in
  `Resources/Raw/Licenses/`). `LockPage` is the app lock (modal; App pushes it on start / `Window.Activated`). `PasswordPage` is
  `IDialogService.PasswordAsync` (modal, hidden entry).
  `SetupPage` is the window's page on a new install
  (swapped for `AppShell` when done) and a pushed route from Settings
- `Charts/` — ChartPalette (light/dark chart colours, categorical slots), ChartFactory (LiveChartsCore donut / columns /
  lines / axes with one look; per-series tap handlers), ChartTapBehavior (drill-down on a real tap, never on a scroll)
- `Controls/` — BottomSheet (the in-page sheet: dim, panel, handle, slide-up; `IsOpen` + `CloseCommand`), PeriodPicker /
  MonthCountPicker / CurrencyChips (bind BindingContext to a page's `Period`; the chips hide with one currency), AddEntryButton (the one add button: opens an Expense /
  Income / Transfer sheet; on Home and Transactions, named `AddEntry`), IOverlayHost (a page with a sheet: Android back
  closes it first),
  LedgerItemTemplateSelector, UpcomingRow (one Upcoming row), FilterChip (`IsActive` → active colours via its style), DecimalEntry (numeric keyboard),
  AutoFitLabel (one line; shrinks its font from `MaxFontSize` to fit: amounts in narrow places, e.g. Home's cards)
- `Behaviors/` — CurrencyMaskBehavior (amount input in the account's currency)
- `Converters/` — one value converter per file (registered in `App.xaml`); InvertedBool, IntToBool and
  IsStringNotNullOrEmpty come from CommunityToolkit.Maui (exceptions suppressed in `MauiProgram`)
- `Services/` — NavigationService, DialogService (alerts/prompts/action sheets on the window's page + toast),
  ThemeService (saved System / Light / Dark → `UserAppTheme`), PreferencesService
  (MAUI Preferences), NoNotificationService / NoDeviceLock / NoReceiptScanner (platforms without reminders / app lock / receipt reading). SecureBackupPasswordStore
  (SecureStorage). ViewModels use `IDialogService` /
  `INavigationService`, never `Shell.Current` or `Toast`
- `Platforms/Android/` — AndroidNotificationService (channels, `POST_NOTIFICATIONS`), DailyReminderWorker (WorkManager
  job once a day at ~09:00: `IReminderService.RunDailyAsync`), AndroidDeviceLock (BiometricPrompt), AndroidBackupFolder (SAF folder picker, persisted tree permission), BalanceWidgetProvider +
  BalanceWidgetConfigureActivity + AndroidHomeScreenWidgets (home-screen widget; redrawn when the app goes to the
  background and after the daily job), AndroidReceiptScanner (ML Kit text recognition, bundled model), MainActivity
  (notification route extra)
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
- `ManageAccountsPage` lists each account's pockets under its card ("Available · in pockets"; a tap opens the pocket BottomSheet: name + icon, set aside / take out, movements, delete); the Goals sheet lists a pocket under its account (an account and its own pockets can't both be ticked)
- Push routes: `AddTransactionPage`, `AddTransferPage` (tab bar hidden) and the More screens `ManageAccountsPage`,
  `ManageCategoriesPage` (Income / Expense switch), `RecurringTransactionsPage`, `GoalsPage`, `PayeesPage`, `TripsPage`, `TripPage`, `TripCategoriesPage`, `PartiesPage` (cards + new/edit sheet), `PartyPage`, `FriendsPage`, `TransfersPage`, `SettingsPage`
- Home links: balance card → `ManageAccountsPage` (`?CurrencyCode=` from a currency line filters it), Recent "See
  all" → `//TransactionsPage`, Upcoming "See all" → `RecurringTransactionsPage?Show=Upcoming`, budgets "See all" →
  `//BudgetPage`, goals card → `GoalsPage`
- Custom `INavigationService` tracks navigation history for back button behavior: AppShell's `Navigated` event feeds
  `NavigationHistory`; Android back → `Shell.SendBackButtonPressed()` → `AppShell.OnBackButtonPressed` (an open sheet
  on an `IOverlayHost` page, then the previous page, then Home, then close)

### Database
- SQLite via `sqlite-net-pcl` with `SQLiteAsyncConnection`
- DB file: `moneyrecord.db3` in `FileSystem.AppDataDirectory`, or `moneyrecord-encrypted.db3` while Settings → Encrypt
  database is on (`IDatabaseEncryption` = `DatabaseInitializer`; key in `IDatabaseKeyStore` = SecureStorage; neither file
  nor key is in Android backup). Native SQLite is SQLite3 Multiple Ciphers: `SqliteSetup.Init` overrides sqlite-net's
  e_sqlite3 provider (DatabaseInitializer's static ctor; tests via a module initializer)
- No ORM relationships — joins done in application code via dictionary lookups
- `DatabaseInitializer` handles schema creation + seed data, then the migration runner
- Seed data: 4 income categories, 7 expense categories, 1 "Cash" account (allows negative balance, device currency)
- **Money is stored as integer cents** (`AmountCents`, `InitialBalanceCents`, `LimitAmountCents`); the `decimal` properties
  are `[Ignore]` wrappers (`Helpers/Money`). Fixed scale of 100 for every currency; zero-decimal currencies (JPY, ...)
  only change display/input (`CurrencyService.GetMinorUnits`); Settings → Show cents hides a currency's cents in display
  only (`GetShownDecimals`; entries still take and show typed cents, `MoneyFormatter.FormatForEntry`)
- **Migrations** (`PRAGMA user_version`): the models describe the latest schema. A new database is created from the models
  and stamped with the latest version; a migration only transforms a database from an older app version (after
  `CreateTableAsync` has added new columns/indexes). Add new migrations at the end of `MigrationRunner.All`; set
  `RewritesData = true` to get a `moneyrecord.db3.bak-v{n}` backup first. Indexes are `[Indexed(Name = "IX_…")]` on the models
- Multi-row changes go through `DatabaseInitializer.RunInTransactionAsync` (deletes in AccountService/CategoryService,
  backup snapshot, restore)
- Totals are summed by SQLite (`SUM(ABS(AmountCents)) … GROUP BY` in the repositories), never by loading every row; converted
  totals too (`SumByDayAsync`: per type, category, currency and day, each day converted at its rate). Only the reports
  load their period's rows (they chart them anyway)
- Balances count the entries dated up to today (`IBalanceService.CountedBefore`): a future-dated transaction or transfer is
  listed (and in its period's totals) but in no balance before its date; what works back from today's balances stops there too
- Backups: `.zip` with `data.json` (format v7, rows keep their IDs; savings goals, pockets and savings challenges included) + `receipts/`; restore snapshots go to `Backups/`

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
- Cards: `<Border Style="{StaticResource Card}">` (pages override Padding / Margin); never `Frame` (obsolete)

## Key Decisions

- Budget is **expense-only** (per-category spending limits, not income targets)
- Transfers are a **separate table** from transactions (linked via TransferId on display)
- Each account has a **currency**; amounts are shown with `MoneyFormatter.Format(amount, currencyCode)` (`MoneyFormatter.BaseCurrency` = the resolved base code, `SameCurrency(a, b)`) / `MoneyConverter`
  / `CurrencyMoneyConverter`. An empty code means the **base currency** (Settings; default = device region currency)
- Never add different currencies together unconverted: totals are per currency (`CurrencyAmount`, SQL `GROUP BY`
  the account's currency; no account = base), or converted into the base with downloaded rates ("≈") when the shared
  "Show in" choice says so (`IConversionService`). Budgets count base-currency expenses, plus converted ones while
  converted
- A transfer stores what left (`AmountCents`, source currency) and what arrived (`DestinationAmountCents`, destination
  currency); they differ only between currencies
- Accounts with transfers can't be deleted; account deletes into a different currency are refused
- Budget progress uses `BudgetLevel` + `BudgetLevelToColorConverter` (light/dark aware); models hold no `Color`
- `BudgetPeriod` enum: Day, Month, Year — projected via `BudgetProjectionHelper`
- `PeriodType` enum for date range filtering: CalendarMonth, CalendarYear, Today, LastWeek, LastMonth, LastYear, CustomPeriod

## CodeGraph

This project is indexed by CodeGraph (`.codegraph/` directory). Use `codegraph_explore` as the primary tool for code exploration — it returns verbatim source, call paths, and blast radius in one call, including dynamic dispatch hops that grep can't follow.
