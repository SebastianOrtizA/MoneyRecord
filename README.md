# MoneyRecord - Personal Finance Tracker

[![Latest release](https://img.shields.io/github/v/release/SebastianOrtizA/MoneyRecord?include_prereleases&label=release&color=512BD4)](https://github.com/SebastianOrtizA/MoneyRecord/releases)

A .NET 10 MAUI app for tracking incomes, expenses and transfers across multiple accounts and currencies, with
budgets, recurring transactions, reports and full backups. Android is the primary target; iOS, macOS (Mac Catalyst)
and Windows are maintained too. All data stays on the device.

## Features

### Home
- **Balance card**: the total balance, with **one line per currency** when accounts use different currencies (never
  added together unconverted). Tap it, or a currency line, to see the accounts behind it.
- **Income / Expenses / Net** for the selected period, plus other currencies' totals below (or one converted total, see
  Currencies).
- **Periods**: this month (default), this year, today, last week, last month, last year or a custom range. Home,
  Transactions, Budgets and the Spending / Income reports share the one period.
- Cards for **recent transactions**, **upcoming** recurring ones, **budgets** and **savings goals**, each with "See all".
- **Hide balances**: the eye on the balance card replaces the amounts with "••••••" (remembered).
- The **+** button opens a sheet: Expense, Income or Transfer.

### Transactions
- **Transactions tab**: the full list grouped by category (default), by account, or flat; newest or oldest first;
  transfers appear alongside transactions. Search (description, category, account, payee, tags) and filter chips for
  type, category, account and amount range. Swipe to edit or delete; a tap opens the details.
- **Fast entry**: the amount first, then a category from a grid that puts your recent ones first; the last account is
  preselected. Description, date, tags, payee, receipt and recurrence are under More details.
- Optional **payee** (shop, person or company): earlier payees are suggested as you type, and picking one selects the
  category it had last time. More → Payees renames, merges (a typo into the right name) and deletes them; search finds
  transactions by payee, and the CSV export has a Payee column.
- **Split an expense** across categories (one receipt for food and household): each part is saved as its own
  transaction, so budgets, reports and totals count it in its category; the parts are edited and deleted together. One
  part left empty takes what is left of the amount.
- **Receipt photos** from the camera or gallery; unsaved photos are discarded when you cancel.
- **Receipt scan** (Android): the button beside the amount reads a receipt photo on the phone (ML Kit text
  recognition) and fills in its total and date.
- Amount input formatted in the **account's currency** (symbol, separators, and no decimals for currencies like JPY).
- Optional **"Make recurring"** to create a recurring rule from the transaction.
- Deleting asks first (and also deletes the receipt photo).

### Accounts
- Multiple accounts (cash, bank, savings, credit card, ...) with an icon, initial balance and **currency**, listed per
  currency with a subtotal and each account's last activity. The **+** button and a tap on a card open the add / edit
  sheet.
- **Allow negative balance** per account (credit cards, debts). The seeded "Cash" account allows it.
- **Safe delete**: transactions and recurring rules move to the default account in one database transaction. Deleting
  is refused when the account has transfers (delete those first) or when the default account uses another currency.

### Transfers
- Move money between accounts, with date, amount and description. Between currencies the form also asks for the
  **amount received** and shows the rate it implies.
- Transfer history (More → Transfers) with edit and delete.

### Categories
- Separate income and expense categories with Material Design icons (More → Categories, Income / Expense switch). The
  **+** button and a tap on a category open the add / edit sheet.
- Seeded on first run (or picked in the setup): Salary, Freelance, Investment, Other Income; Food, Transportation,
  Entertainment, Utilities, Shopping, Home, Other Expense.
- **Safe delete**: transactions, recurring rules and the budget move to a replacement category (the budget is deleted
  if the replacement already has one), after a confirmation that says what will move.

### Budgets
- Spending limits per **expense** category, per day, month or year, projected to the selected period
  (a daily budget of $10 shows as $310 for a 31-day month).
- Each card says **On track**, **Near limit** or **Over budget**, with light/dark-aware colours; tap it to change the
  limit.
- Totals: budgeted, spent, remaining.

### Savings goals
- A goal is a target amount, optionally by a date, saved in one or more accounts (More → Goals). Saved = those
  accounts' balance; an account counts for one goal only, and a goal's accounts share its currency.
- Each goal shows what to save per month to reach it by its date, and when the last 3 months' pace would reach it
  (On track / Behind / Reached). Home lists the first three.

### Debts and loans
- Money lent to or borrowed from a person (More → Debts): what is owed in each direction, paid back so far, and an
  optional due date (reminded before it, with the bill reminders). A debt that was owed before you started counts too.
- Payments are recorded as they come, optionally with an **interest** part (saved as an expense or income in an
  Interest category). Lending, borrowing and payments move money between your accounts and the debt, so balances and
  reports stay right.
- **Payments on a schedule** (amount + frequency + account): each one is created like a recurring transaction, the last
  is capped at what is left, and the schedule stops when the debt is paid, with a notification.

### Recurring transactions
- Daily, weekly, biweekly, monthly (optionally on a specific day) or yearly rules, with an optional end date.
- Due transactions are created when the app starts and, on Android, by a daily background job.
- **Upcoming**: the next 30 days of scheduled transactions, on Home and on the Recurring page.
- **Reminders** (Android, opt-in): a notification before a bill is due (on the day or 1–7 days before), when a
  budget reaches 80% of its limit or goes over it, and when the daily job adds recurring transactions. Set them in
  **Settings → Reminders**; the app offers them once after you add your first recurring transaction or budget.

### Reports
- **Spending / Income**: a donut chart and a ranked list by category (the 8 largest keep their own colour, the rest are
  grouped under "Other"), with the change against the previous period. Tap a slice or a row for its transactions.
- **Trends** (monthly income and expenses, budget vs. actual), **Cash flow** and **Net worth** over the last 3, 6 or 12
  months.
- **Export**: a PDF report of the period (summary, categories, budgets, balances and entries) or the period's
  transactions as CSV, shared from the Reports tabs.

### Currencies
- **Base currency** (default: the device region's), used for transactions without an account and for converted totals.
- Amounts show a plain symbol in front ("$1,200.00"), plus the code where the symbol alone isn't clear ("$1,500.00 USD"
  when the base is another dollar or peso, and in lists of several currencies).
- **Show cents** per currency (Settings → Base currency): off rounds that currency's amounts to whole units on screen;
  they are still saved, and typed, to the cent.
- **Exchange rates** (opt-in): daily rates downloaded from [Frankfurter](https://frankfurter.dev) let Home, the
  reports and the budgets show one converted total ("≈"). The **Show in** choice switches between converted and
  per-currency totals; a transaction can keep its own converted amount.

### Settings, backup & restore
- **Full backup** as a `.zip` (`data.json` + receipt photos), saved locally or shared.
- **Backup password** (opt-in): backups, manual and automatic, are AES-256 encrypted zips (7-Zip and other zip tools
  open them with the password). The password is kept in SecureStorage for the automatic backups and asked for on
  restore when it's missing or different.
- **Automatic backups** (Android, opt-in): daily, weekly or monthly into a folder picked with the system folder picker
  (phone storage, SD card, or a cloud app such as Google Drive), keeping the newest 3–20; skipped while nothing
  changed.
- **Restore** from a local or picked backup (also older `.json` backups). The restore is all-or-nothing; the current data
  is saved to an automatic snapshot first (the last 5 are kept) and the "Restore complete" dialog offers **Undo**.
- **CSV export** of all transactions, and **CSV import** of a bank statement into one account: pick the columns, check a
  preview that marks duplicates and suggests categories learned from your history, and undo the import if needed. The
  columns are remembered per statement layout.
- **App lock** (Android, opt-in): fingerprint, face or the phone's screen lock when the app opens and after 1, 5, 15
  or 60 minutes in the background. The app stores no PIN or biometric data; on Android 13+ the recent apps screen
  doesn't show its content while the lock is on.
- **Encrypt database** (Android, opt-in): the database is encrypted (SQLite3 Multiple Ciphers) with a random key kept
  in the Android Keystore through SecureStorage. While it is on, Android backup leaves the database out (the key can't
  leave the phone), so move to a new phone with a backup from **Settings → Backup** (protect it with the backup password).
- **Home-screen widget** (Android): a picked account's balance with Expense / Income / Transfer buttons; hidden while
  Hide balances or the app lock is on.
- **First-run setup**: language, base currency, starting accounts and categories (it can be run again from Settings).

### Localization
- English and Spanish, picked from the device language or chosen in Settings; the device region is kept for currency
  and number formats (e.g. `es-CO`).
- Dates use the culture's own patterns ("October 11, 2026" / "11 de octubre de 2026"), and counts use proper plurals.

### Navigation
- Bottom tabs: Home · Transactions · Reports (Spending / Income / Trends / Cash flow / Net worth) · Budgets · More (the
  other screens). A tab always opens at its own page, also when tapped again.
- The Android back button closes an open sheet first, then goes back, then to Home, and closes the app from Home.

### Theming
- Full light and dark mode, following the system or picked in Settings.

## Data & storage
- SQLite (`moneyrecord.db3` in the app's data folder, or `moneyrecord-encrypted.db3` while encrypted) via `sqlite-net-pcl`
  on SQLite3 Multiple Ciphers (`SqliteSetup`).
- **Money is stored as integer cents** (`AmountCents`, ...), so totals never drift.
- **Schema migrations**: `PRAGMA user_version` plus a migration runner; each migration runs in its own transaction, and
  migrations that rewrite data back up the database first (`moneyrecord.db3.bak-v{n}`).

### Privacy
The full privacy policy (English and Spanish) is in [PRIVACY.md](PRIVACY.md), also published at
https://sebastianortiza.github.io/MoneyRecord/privacy.

- **Offline:** your data never leaves the device unless you export or share a backup yourself. The internet permission
  is there for opt-in crash reports and exchange rates, and ML Kit's diagnostics when you scan a receipt (below).
- **No accounts, ads, analytics or tracking** of its own (ML Kit's diagnostics are described below).
- **Crash reports (opt-in, off by default):** after a week of use the app asks once whether it may send crash reports
  to the developer through [Sentry](https://sentry.io), and **Settings → Send crash reports** changes the answer.
  A report holds the error, the app version, device details such as the Android version and model, and the
  approximate location (country and city) that Sentry derives from the connection. The app doesn't add your amounts
  or descriptions, screenshots, or a user or device ID, and the Sentry project doesn't store IP addresses (that relies
  on **Prevent Storing of IP Addresses** being on in the project's Security & Privacy settings; turn it on again if
  the project is ever recreated).
  Only official builds can send reports; builds from source have no Sentry address and never ask.
- **Receipt reading (Android):** a scanned receipt is read on the phone by Google's ML Kit (model inside the APK; no
  Play Services needed). The photo and its text stay on the device, but ML Kit sends Google usage diagnostics (device
  and app info, a per-install ID, performance metrics; no image), as its
  [data disclosure](https://developers.google.com/ml-kit/android-data-disclosure) describes. ML Kit is under
  [Google's terms](https://developers.google.com/ml-kit/terms), not an open-source licence.
- **Exchange rates (opt-in, off by default):** **Settings → Exchange rates** downloads daily rates from
  [Frankfurter](https://frankfurter.dev); a request holds only currency codes and dates, never your amounts.
- **Permissions:** the camera, only when you take a receipt photo. On Android 12 and older, the storage-write permission
  is also declared, because the camera capture needs it there. Picking a photo or a backup file uses the system pickers,
  which need no permission. Notifications (Android 13 and newer), only when you turn reminders on. Biometrics, only
  when you turn the app lock on (Android checks it; the app only learns whether it passed).
- **Logs:** errors and basic app info (version, device OS) are written to log files on the device (the last 7 days,
  at most 10 MB in all). They hold technical details such as error messages (the app doesn't log your amounts or
  descriptions), and leave the device only when you use
  **Settings → Share logs**.
- **Android backup:** if the device's own backup is on, it copies the database, receipt photos and settings, which
  includes device-to-device transfer. Restore snapshots, migration copies, logs, caches, the encrypted database and
  SecureStorage (its key) are left out
  (`Platforms/Android/Resources/xml`). Google's cloud backup holds at most 25 MB per app; past that it stops backing
  up the app, so use **Settings → Backup** for a complete copy.

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

## Releases

CI (`.github/workflows/build-android.yml`) builds a Release `.apk` (GitHub Releases / F-Droid) and `.aab` (Play
Store) on every push. A `v*` tag publishes them as a GitHub Release, and that build fails unless the release keystore
is configured, so a published APK is never signed with the debug key.

The tag build also uploads the `.aab` to Google Play's **closed testing** track (`alpha`; set the repository variable `PLAY_TRACK`
for a custom-named closed track), with the release notes in
`fastlane/metadata/android/<locale>/changelogs/default.txt` (update them before tagging; 500 characters at most), and add the release to `history.txt` in the same folders (the in-app "What's new" lists every version since the last one shown).
Promoting it to production is done in Play Console. The upload uses the
`PLAY_SERVICE_ACCOUNT_JSON` secret: the JSON key of a Google Cloud service account (Google Play Android Developer API
enabled) invited in Play Console → Users and permissions with the release-to-testing permissions. Without the secret
the upload is skipped. While the app has never been published, Play only accepts draft releases: set the repository
variable `PLAY_RELEASE_STATUS` to `draft` until then.

Versions: the version name comes from the tag (`v1.2.0` → `1.2.0`; a suffix such as `v0.9.0-rc1` is kept and marks
the GitHub Release as a pre-release) and the version code is the workflow run number, so every CI build can update
the previous one. Untagged builds show the csproj's `ApplicationDisplayVersion` (for example `1.0.0`) in About, however
many builds there have been; bump it after a release if testers need to tell builds apart. To release, push a tag:
`git tag v1.2.0 && git push origin v1.2.0`.

The release key signs every update: an APK signed with another key can't be installed over the app without losing its
data. Create it once and keep a backup of the file and both passwords outside the repository (`*.keystore` is ignored):

```bash
keytool -genkeypair -keystore moneyrecord.keystore -alias moneyrecord -keyalg RSA -keysize 4096 -validity 10000
```

Then store it in the repository secrets (PowerShell):

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("$PWD\moneyrecord.keystore")) | gh secret set ANDROID_KEYSTORE_B64
gh secret set KEYSTORE_PASSWORD
gh secret set KEY_PASSWORD   # the same as the store password for keytool's default PKCS12 keystore
```

To sign a local Release build, put the file at `MoneyRecord/moneyrecord.keystore` and set `KEYSTORE_PASSWORD` and
`KEY_PASSWORD` in the environment.

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
├── Models/             Account, Transaction, Transfer, Category, Budget, RecurringTransaction, SavingsGoal, Payee, Debt (tables)
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
│   ├── SavingsGoalService                       Goal progress (linked accounts' balances) + projection
│   ├── PayeeService                             Payees: found or created on save, use counts, rename / merge / delete
│   ├── SplitService                             Saves a split expense as one transaction per category
│   ├── DebtService, DebtLedger                  Debts: amounts, payments (+ interest), schedule, delete
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
├── Views/              Main, AddTransaction, AddTransfer, Transfers, ManageAccounts, ManageCategories, Budget, Goals,
│                       Payees, RecurringTransactions, CategoryReport (Expense/Income), TrendsReport, Settings
├── App.xaml            Converters and styles; processes recurring transactions on start
├── AppShell.xaml       Flyout menu and routes
└── MauiProgram.cs      Dependency injection
MoneyRecord.Tests/      xUnit tests (net10.0, Core only): helpers, models, services, view models, migrations, backup/restore,
                        resource parity
```

## License

MoneyRecord is licensed under the **GNU Affero General Public License v3.0** (see [LICENSE](LICENSE)).

Third-party components shipped in the app, with their licences: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) (the same
list as Settings → About, which shows the full licence texts from `MoneyRecord/Resources/Raw/Licenses/`). A test keeps
the file, the About list and the package references in step; transitive Android dependencies are checked by hand
after a MAUI, SkiaSharp or Sentry update. Test-only packages: xUnit (Apache-2.0), NSubstitute (BSD-3-Clause),
AwesomeAssertions (Apache-2.0), coverlet (MIT).
