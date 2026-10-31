# MoneyRecord

.NET 10 MAUI personal finance app (incomes, expenses, transfers, budgets, accounts). Android is the primary target (APK); iOS, macCatalyst and Windows are maintained. License: AGPL-3.0. Solution: `MoneyRecord.slnx`.

## Build & Run

```bash
dotnet build MoneyRecord/MoneyRecord.csproj -f net10.0-android
dotnet build MoneyRecord/MoneyRecord.csproj -f net10.0-windows10.0.19041.0
dotnet build MoneyRecord/MoneyRecord.csproj -t:Run -f net10.0-android   # emulator/device
dotnet test MoneyRecord.Tests/MoneyRecord.Tests.csproj                  # xUnit + NSubstitute + AwesomeAssertions, plain net10.0, run from PowerShell
```

- Release builds use R8 (rules: `MoneyRecord/Platforms/Android/proguard.cfg`; mapping.txt next to the APK/AAB). A Java class used by name that R8 renames/removes fails only at runtime: smoke-test a Release APK after adding Android libraries.
- Two Android distributions: default "Direct" (GitHub APK / F-Droid: no Support screen, `NoTipJar`) and `-p:Distribution=Play` (AAB: defines PLAY_STORE, adds Plugin.InAppBilling, `PlayTipJar`; Play requires its billing for developer payments). Test billing with a Play-installed build and a license-tester account, never the Direct one.
- Emulator: project skill `/run-moneyrecord`. Release: skill `/release-moneyrecord` (notes in all locales, PR, Actions → Cut release; fixes backported to `release/x.y` by PR label; tags only from release branches; every release adds a `## x.y.z` section to each `changelogs/history.txt` and updates Play's `default.txt`). Translations: `/translate-moneyrecord`.
- Staged roadmap plans: `.claude/plans/` (one file per stage).

## Architecture

Models (SQLite entities, money in cents) → Repositories (`IRepository<T>` + migrations) → Business services → ViewModels (CommunityToolkit.Mvvm) → Views (XAML + code-behind).

Two projects with the same namespaces (`MoneyRecord.Models`, `MoneyRecord.Helpers`, ...):
- `MoneyRecord.Core/` — plain `net10.0`, no MAUI, all testable logic: `Models/` (SQLite tables + display models), `Services/Interfaces/`, `Services/` (business services), `Services/Repositories/` (`RepositoryBase<T>`, one repo per table, `DatabaseInitializer`, `Migrations/` with `MigrationRunner.All`), `Helpers/`, `ViewModels/` (MAUI-free), `Resources/Strings/` (AppResources.resx + es/pt/fr/it).
- `MoneyRecord/` — the MAUI app: `ViewModels/`, `Views/`, `Charts/` (LiveChartsCore: ChartFactory, ChartPalette), `Controls/` (BottomSheet, IOverlayHost, EmptyState, AutoFitLabel, ScaledLayout, ...), `Behaviors/`, `Converters/` (one per file, registered in `App.xaml`), `Services/` (Navigation, Dialog, Theme, Preferences, `No*` fallbacks for platforms), `Platforms/Android/` (notifications, WorkManager daily job, biometric lock, SAF backup folder, widget, ML Kit receipt scanner), `MauiProgram.cs` (DI).
- `MoneyRecord.Tests/` — references Core only; real temp SQLite files for repository/migration/backup tests; runs on Linux (CI uses ubuntu-latest).

Use `codegraph_explore` to find where a feature lives rather than relying on a file list here.

XAML that uses a Core type needs `xmlns:models="clr-namespace:MoneyRecord.Models;assembly=MoneyRecord.Core"` — without `assembly=` a Debug build compiles but crashes at runtime.

## Conventions

### MVVM
- `ObservableObject`; `[ObservableProperty]` (field `propertyName` → `PropertyName`), `[RelayCommand]` (`XxxAsync` → `XxxCommand`), `partial void OnPropertyNameChanged(T value)`.
- Reloads: one `[RelayCommand(AllowConcurrentExecutions = true)]` load taking a `CancellationToken`; a new run cancels the previous one (check the token after awaits, catch cancellation, only the last run clears the spinner). Property-changed handlers call `LoadXCommand.Execute(null)`, never `_ = LoadXAsync()`.
- Views set `BindingContext` in the constructor via DI and call `ViewModel.InitializeAsync()` in `OnAppearing()`.
- ViewModels use `IDialogService` / `INavigationService`, never `Shell.Current` or `Toast`. `ErrorHandler` shows a localized message and logs the exception; never show `ex.Message`.

### DI (MauiProgram.cs)
Services and repositories Singleton; pages and ViewModels Transient (except MainPage + MainViewModel Singleton). Register interface → implementation.

### Everyday UX
- Deleting a transaction/split/transfer has no confirm: `ILedgerService.DeleteAsync` returns a `DeletedEntry`, and `IDialogService.ToastWithUndoAsync` offers Undo (`RestoreAsync`); receipt files go only when the snackbar expires. Heavy deletes (account, category, ...) still confirm.
- Empty lists use the shared `EmptyState` control (icon, title, hint). New installs see the Home get-started card (`GetStartedChecklist`).

### Navigation
- Shell `TabBar`: Home · Transactions · Reports (Spending / Income / Trends / Cash flow / Net worth top tabs) · Budgets · More. Tab pages are roots at `//MainPage`, `//TransactionsPage`, `//ExpenseReportPage`, ... `//MorePage`. Other screens are pushed routes (Add Transaction/Transfer hide the tab bar; More lists the rest).
- `INavigationService` tracks history for the back button: AppShell `Navigated` → `NavigationHistory`; Android back → `AppShell.OnBackButtonPressed` (open sheet on an `IOverlayHost` page, then previous page, then Home, then close).

### Database
- SQLite via `sqlite-net-pcl`; no ORM relationships (joins in application code via dictionaries). File `moneyrecord.db3` (or `moneyrecord-encrypted.db3` with SQLite3 Multiple Ciphers when Encrypt database is on; neither file nor key is in Android backup).
- **Money is integer cents** (`AmountCents`, ...); `decimal` properties are `[Ignore]` wrappers (`Helpers/Money`). Scale is always 100; zero-decimal currencies and Settings → Show cents only change display/input (`GetShownDecimals`, `MoneyFormatter.FormatForEntry`).
- **Migrations** (`PRAGMA user_version`): models describe the latest schema; a new DB is created from models and stamped latest. A migration only transforms older DBs (after `CreateTableAsync` adds columns/indexes). Append to `MigrationRunner.All`; `RewritesData = true` makes a `.bak-v{n}` backup first. Indexes: `[Indexed(Name = "IX_…")]`.
- Multi-row changes go through `DatabaseInitializer.RunInTransactionAsync`.
- Totals are summed by SQLite (`SUM … GROUP BY`), never by loading every row; only reports load their period's rows.
- Balances count entries dated up to today (`IBalanceService.CountedBefore`); future-dated entries are listed and in period totals but in no balance before their date.
- Backups: `.zip` with `data.json` (format v8, rows keep IDs) + `receipts/`; restore snapshots go to `Backups/`.

### Localization
- `.resx` with `ResXFileCodeGenerator`. XAML: `{ext:Localize Key}`. Code: `AppResources.Key` or `LocalizationService.Instance.GetString(key)`.
- Languages are the rows of `AppLanguage.All` (en, es, pt-BR, fr, it): a new one = a row there + `AppResources.xx.resx` + widget `values-xx` + `fastlane` folder (tests fail until all exist).
- `ResolveCulture` keeps the device region (`es` + `CO` → `es-CO`), never a neutral culture.
- Dates via `DateFormatter` / `DateTextConverter`, never fixed patterns. Counts via `Plurals` (`...One` / `...Other` keys). Filters and logic compare enums/IDs, never display strings.
- `ResourceParityTests` fail on a missing key, translation or `{n}` placeholder.

### Icons
Material Design Icons font (alias "MaterialDesignIcons"); hex codes stored as strings (`"F0770"`); only `IconHelper` parses them. Defaults: category `F0770`, account `F0070`.

### Theming
Light/dark via `AppThemeBinding`; colors in `Resources/Styles/Colors.xaml`; primary `#512BD4`. Cards: `<Border Style="{StaticResource Card}">`, never `Frame`.

## Key Decisions

- Budget is expense-only (per-category limits). Transfers are a separate table from transactions.
- Each account has a currency; format with `MoneyFormatter.Format(amount, currencyCode)` / `MoneyConverter` / `CurrencyMoneyConverter`. Empty code = base currency (Settings; default = device region currency).
- Never add different currencies unconverted: totals are per currency (`CurrencyAmount`, SQL `GROUP BY` currency), or converted into the base with downloaded rates ("≈") when "Show in" says so (`IConversionService`).
- A transfer stores what left (`AmountCents`) and what arrived (`DestinationAmountCents`); they differ only across currencies.
- Accounts with transfers can't be deleted; deletes into a different currency are refused. Hidden accounts (`Account.IsDebt`) are left out of pickers via `IAccountRepository.GetOwnAsync`.
- Budget colors via `BudgetLevel` + `BudgetLevelToColorConverter`; models hold no `Color`.
- Split expenses are plain transactions sharing `Transaction.SplitId`. Pockets are earmarks inside an account (signed `PocketMovement`s). Recurring variable-amount schedules are never auto-created: each due date is a pending `UpcomingEntry`.
