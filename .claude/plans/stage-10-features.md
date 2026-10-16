# Stage 10: New Features

**Status**: In progress (10.1 done; 10.2 done; 10.3 done; 10.4 done; 10.5a in review)
**Depends on**: Stages 6–8 (9 for anything needing network permission)
**Complexity**: Variable (each item is independent; order = value)
**Source**: Product & engineering review, 2026-10-11 (P10, P11, "What to add") + Stage 5.2, 5.5

## Goal
Close the gap with competing finance apps. Each section can ship on its own.

## Tasks (in priority order)

### 10.1 Reminders and background recurring (P10)
- Notifications via Plugin.LocalNotification (MIT): bill due (recurring rule, N days before), budget at 80% / 100%,
  "N recurring transactions added".
- Process recurring rules in the background daily (Android WorkManager via Shiny, MIT), not only at app start.
- "Upcoming" list on Home / Recurring: next 30 days of scheduled entries.
- Android 13+ `POST_NOTIFICATIONS` runtime permission prompt with explanation.
- Split into two PRs (2026-10-14): **10.1a** = the Upcoming list; **10.1b** = notifications, the daily background
  job and the permission prompt.
- 10.1a done as: Core `UpcomingSchedule` (occurrences from each active recurrence's next occurrence through today +
  30 days, by `RecurrenceCalculator`, up to the end date; overdue ones not created yet are listed as "Today"
  (review)) + `UpcomingEntry` ("Today" / "Tomorrow" / "In N days" via `Plurals.InDays`);
  `IRecurringTransactionService.GetUpcomingAsync`. Home has an Upcoming card (next 3, hidden when empty; "See all" →
  `RecurringTransactionsPage?Show=Upcoming`); the Recurring page has a Schedules / Upcoming switch. Rows are one
  shared `Controls/UpcomingRow`; the Recurring page builds the list from the rows it already loaded (review). A test
  checks the Upcoming dates equal what processing creates 30 days later.
- 10.1b done as: Core `ReminderService` (daily job: bill reminders for expenses due within the chosen days, before
  processing so today's still count; processing; "N recurring transactions added"; budget alerts at 80% (High) and
  over the limit, once per budget period each), `ReminderSettings` (off until turned on; bills 1 day before, budget
  alerts and "added" on), `INotificationService`, `NotificationLaunch` (a tapped notification's page: at once when
  running, else when Home appears), `BillReminderOption`. Android: `AndroidNotificationService` (channels Bills /
  Budgets, bell icon, `POST_NOTIFICATIONS` via MAUI `Permissions.PostNotifications`), `DailyReminderWorker`
  (WorkManager periodic job aimed at 09:00, fixed Java name, scheduled at every start with KEEP). Other platforms:
  `NoNotificationService` (Settings hides the card). The offer ("Get reminders?") comes once after adding a first
  recurring transaction or budget; Settings → Reminders has the switch, bill days, budget alerts and "added".
  Saving an expense checks the budgets right away. `ProcessDueTransactionsAsync` runs one at a time (app start and
  the job can overlap).
  **Deviation:** no Plugin.LocalNotification / Shiny: AndroidX WorkManager (`Xamarin.AndroidX.Work.Runtime` 2.10.3,
  the newest without Lifecycle conflicts with MAUI's AndroidX) + `NotificationCompat` from AndroidX.Core directly.
  Known limit: if the app starts before the job on a bill's own day, it creates the occurrence first, so an "On the
  day" reminder for it is skipped (the 1-day-before default is not affected).

### 10.2 App lock and encryption (P11)
- Biometric/PIN lock on open and after N minutes in background (Oscore.Maui.Biometric, MIT. Check its NOTICE,
  it's derived from MS-PL code; fallback: direct AndroidX.Biometric binding).
- Encrypt the DB with SQLCipher (`SQLitePCLRaw.bundle_e_sqlcipher`); key in Android Keystore via `SecureStorage`.
  Migration: export → re-create encrypted DB → import; keep a pre-migration backup.
- Hide balances toggle (privacy mode) on Home.
- Split by agreement (2026-10-15): **10.2a** = app lock + hide balances; **10.2b** = SQLCipher (options on the
  package and the Android backup trade-off first). The user chose the phone's own lock (BiometricPrompt with
  fingerprint / face / screen lock) over an app PIN.
- 10.2a done as: Core `AppLock` (locks at start and after 1 / 5 / 15 / 60 minutes away; never without a phone lock, so
  removing the screen lock can't lock the user out), `PrivacySettings` (lock off by default, 1 minute, hide balances),
  `IDeviceLock`, `LockDelayOption`. Android `AndroidDeviceLock` (`Xamarin.AndroidX.Biometric` 1.1.0.30, the newest
  without Lifecycle NU1608; BIOMETRIC_WEAK | DEVICE_CREDENTIAL; recent apps screenshot off on Android 13+), modal
  `LockPage` (back leaves the app: a modal page gets back before the Shell), checked on `Window.Activated` (Resumed
  comes before the activity is in front and cancels the prompt). Settings → App lock (asks the phone's lock once to
  turn it on; "No screen lock" alert). Home eye toggle masks the balance card and the period totals.
  **Deviation:** no Oscore.Maui.Biometric (AndroidX Biometric directly, like WorkManager in 10.1b); no "immediately"
  delay (a receipt photo, file pick or share leaves the app for a moment). Recent / Upcoming / budget amounts on Home
  stay visible (the toggle hides balances and totals).
- 10.2b options (2026-10-15): `SQLitePCLRaw.bundle_e_sqlcipher` stops at 2.1.11 (SQLitePCLRaw 3 dropped it) and
  Zetetic's SQLCipher for .NET is commercial, so the cipher is **SQLite3 Multiple Ciphers** (`SQLite3MC.PCLRaw.bundle`
  2.6.1, MIT, built on SQLitePCLRaw 3, 16 KB-aligned Android libraries). The user chose **opt-in encryption with the
  key in the Android Keystore** over always-on, a key that travels with the backup, or no encryption.
- 10.2b done as: Settings → Encrypt database (Android; off by default). `DatabaseInitializer` implements
  `IDatabaseEncryption`: encrypting writes a checked copy (`PRAGMA rekey`, file header + `integrity_check`) to
  `moneyrecord-encrypted.db3`, a name Android backup doesn't include, then deletes the plain file and its
  `.bak-v*` migration copies; decrypting does the reverse and forgets the key. The state is which file exists (no
  preference to restore out of sync). The key (32 random bytes, hex) is in `IDatabaseKeyStore` = MAUI SecureStorage,
  whose file is excluded from Android backup (MAUI's own advice). A start that finds the encrypted file without a
  working key sets it aside (`.unreadable-{time}`; only "file is not a database" counts, other errors are thrown so the next start retries), starts from a new database and tells the user on Home.
  `SqliteSetup.Init` swaps sqlite-net's provider for SQLite3MC after sqlite-net's static constructor (which always
  picks e_sqlite3, so plain SQLite still ships: about +1 MB per ABI).
  **Deviation:** no export → re-create → import migration and no kept plain pre-migration copy: the encrypted copy is
  written and checked next to the original before the original is deleted (a plain copy would defeat the point).
  Known limits: zip backups and the pre-restore snapshots (`Backups/`) stay unencrypted (encrypted zips belong to
  10.5); a query running at the moment the switch closes the connection can fail (the switch is a Settings action).

### 10.3 Multi-currency done properly (P2)
- Base currency (from Stage 6.5) + exchange rates from Frankfurter API (MIT, ECB rates, no key, history since 1999).
  Cache daily rates in a `ExchangeRate` table; manual rate override per transaction.
- Transfers store `SourceAmountMinor` and `DestinationAmountMinor` (+ rate) for cross-currency transfers.
- Totals and reports convert to base currency at the transaction-date rate; label converted values.
- **"Show in" switch** on Home and in Reports: *Base currency* (one converted total, values marked "≈", rate date
  shown on tap) or *Original currencies* (the per-currency subtotals from Stage 6.5, no conversion). Remember the
  choice in preferences; default = Original until rates have been downloaded once.
- Needs `INTERNET` (opt-in toggle "Download exchange rates").
- **Moved here from the 6.5 review (PR #9, item 5):** these totals still add amounts across currencies and label the sum
  in the base currency: Home Incomes/Expenses/Net (`BalanceService.GetTotalIncomesAsync`/`GetTotalExpensesAsync`),
  report category amounts and totals, budget spent/remaining, category group totals (`TransactionGroup` with mixed
  currencies) and chart values. Until rates exist they should at least show per-currency subtotals the way
  `MoneyFormatter.FormatTotals` does in the report detail popup (already fixed in 6.5).
- Split by agreement (2026-10-15): **10.3a** = cross-currency transfers (source + destination amounts; the rate
  follows from them; a migration fills the destination amount of existing transfers) + per-currency subtotals
  wherever totals mix currencies today (Home, reports, budgets, groups). No network. **10.3b** = `ExchangeRate`
  table, Frankfurter v2 download (opt-in; v2 has several providers, BANREP covers COP), the "Show in" switch,
  converted totals marked "≈".
- Decisions (2026-10-15): the manual rate override is **per transaction** (an optional rate in the add/edit form's
  More details, stored on the row; 10.3b). Budgets: before rates exist (or with "Show in" = Original) only
  base-currency expenses count and the card says so; with rates, other currencies count converted at the
  transaction-date rate. Reports (before rates): **currency chips** under the period picker, one currency at a time,
  shared by every report tab (10.3b adds a converted "≈ All in base" choice).
- 10.3a done as: `Transfer.DestinationAmountCents` (migration v4 `M004_TransferDestinationAmounts` and restore fill it
  with the source amount; backup format v4); the destination balance adds it. The transfer form asks "Amount received"
  when the currencies differ and shows the rate (`TransferRate`: "1 USD = 4,012.5 COP"); the old mismatch warning is
  gone. `LedgerEntry.CounterpartAmountCents/CurrencyCode`: each half in its own account's currency, a whole transfer
  shows "COL$401,250.00 / → $100.00". Totals: `ITransactionRepository.SumByCurrencyAsync` / `SumByCategoryAsync` (SQL
  `GROUP BY` the account currency; no account = base) return `CurrencyAmount` lists; `IBalanceService` period totals
  are per currency. Home: the three cards show the base currency, other currencies are tinted pills under them, all in one
  lighter `PeriodSummary` container (`PeriodTotals`; hidden with the balances; review). Transactions groups list one total per currency (`TransactionGroup.Totals`).
  Budgets count base-currency expenses only (`BudgetProgress.SetSpent`; "Not counted: …" on the Budget page card;
  also the add-form warning and the reminder job). Reports: `PeriodViewModel.ReportCurrency` (its own
  `CurrencyChanged` event, watched by the reports only; review) + `CurrencyChips` (hidden with one currency; a
  currency no account uses falls back to base); `ReportCurrencies.InUse/Only` (the one transaction-currency rule, used
  by every report; review); `MoneyFormatter.BaseCurrency` / `SameCurrency` (review; `FormatTotals` removed); chart
  axes and tooltips format in that currency; Trends hides budget vs actual for a non-base currency; Net worth counts
  that currency's accounts.
  **Deviation:** no stored rate column on `Transfer` (it follows from the two amounts). Home's compact budget card
  doesn't repeat the "Not counted" line (the Budget page has it).
- 10.3b split by agreement (2026-10-15): **10.3b-1** = `ExchangeRate` table, Frankfurter v2 download (opt-in switch +
  "Update now" in Settings; at start and in the daily job), a Core converter and the per-transaction override; nothing
  on screen converts yet. **10.3b-2** = the "Show in" switch (converted Home total, "≈ All in base" report chip,
  budgets counting other currencies converted).
- Decisions (2026-10-15): the override is entered as **"Amount in USD"** (the base currency; what a bank statement
  shows), with the rate shown under it like the transfer form; stored as the converted amount + its currency, so a
  later base change ignores it. A day without a rate uses the **nearest earlier day**; an entry older than every
  downloaded rate makes the download go back to its date; still none (offline) = listed unconverted.
- Frankfurter v2 (checked 2026-10-15): `GET https://api.frankfurter.dev/v2/rates?base=USD&quotes=COP,EUR&from=…&to=…`
  returns `[{"date":"2026-10-01","base":"USD","quote":"COP","rate":3317.65}, …]`, weekends included; no key, no quota
  (rate-limited against abuse); blended rates free to use.
- 10.3b-1 done as: `ExchangeRate` table (date, base, quote, `double` rate; unique (base, quote, date) index, upsert; a
  cache, not in backups). `IExchangeRateSource` = `FrankfurterRateSource` (HttpClient, 30 s timeout; 404/422 =
  `ExchangeRateUnavailableException`). `ExchangeRateService.UpdateAsync`: for each account currency other than the base,
  from its first transaction (`ITransactionRepository.GetFirstDatesByCurrencyAsync`) or today, to today, only the days
  missing before/after the stored ones; one update at a time; `LastUpdated` set unless a download failed (offline =
  retried). `UpdateIfDueAsync` (on + not today) runs at app start and in the daily WorkManager job;
  `UpdateIfEnabledAsync` (on, any time) runs in the background after saving an account or an entry in another currency,
  so a new currency or an older date gets its rates at once (review). A leading range that came back empty isn't asked
  again while the app runs (review).
  `CurrencyConverter` (Core): nearest earlier day, inverse pairs and across an earlier base (rates stay usable after a
  base change), `Convert(Transaction, …)` prefers the user's own conversion in that currency. Settings → Exchange rates
  (switch; turning it on downloads; status line + Update now). `Transaction.ConvertedAmountCents` +
  `ConvertedCurrencyCode` (new nullable columns, no migration; `SetConversion` rule: an empty field clears only a
  conversion into the current base); the add/edit form's More details shows "Amount in USD" + the rate when the
  account isn't in the base. PRIVACY.md / README: the opt-in download sends currency codes and dates only.
  Backup format stays v4 (the override is two optional fields; 10.3a's v4 hasn't shipped yet).
- 10.3b-2 done as: one shared "Show in" choice (`ICurrencyDisplaySettings`, unset = converted once rates exist):
  Home's "≈ USD / By currency" switch and the reports' first chip "≈ USD" set it. Core `ConversionService`
  (`CanConvertAsync` = download on + rates stored; `IsConvertedAsync`; `ConvertAsync` per transaction at its date's
  rate, own "Amount in USD" first; `ForReportAsync`; `GetPeriodTotalsAsync` for Home; `GetExpensesByCategoryAsync` →
  `BudgetSpent` for the Budget page, Home card, add-form warning and budget alerts). Home converted: one "≈" total in
  the cards (the mark on the captions, so a long amount keeps its line), the pills list what had no rate yet under
  "No exchange rate yet", and "≈ At each day's exchange rate · updated …". Reports converted: every currency in the
  base, headline "≈ …", "No exchange rate yet: …" note; Trends compares budgets; Net worth values each currency at
  each month end's rate (`NetWorthBuilder.BuildConverted`; a currency missing a rate is left out and named). Rates now
  always reach back to the first of the month 12 months ago (Net worth's longest window), not just the first entry.
  **Deviation:** "rate date shown on tap" became an always-visible note under the converted totals (one tap less).
  Review: the "several currencies" rule lives in `ConversionService.GetStateAsync` (`ConversionState`); converted
  Home / budget totals come from SQL day sums (`ITransactionRepository.SumByDayAsync`), not loaded rows;
  `MoneyFormatter.ApproximateMark`; `PeriodViewModel` writes the choice through `IConversionService`. Owner request:
  `Controls/AutoFitLabel` shrinks Home's amounts (balance lines, cards, pills) to fit one line, so COP millions
  ("$ 7.771.515,00") no longer wrap.

### 10.4 Import from bank statements
- CSV import with CsvHelper (use its Apache-2.0 licence): pick file → map columns (date, description, amount,
  type/sign) → preview → duplicate detection (date + amount + description) → import into one account.
- Save mappings per bank. Later: OFX/QIF.

  Split by agreement (2026-10-16): **10.4a** = import, **10.4b** = saved mappings per bank (recognised by the header).
  Owner choices: one signed amount column (no debit/credit or type columns); categories learned from history (the
  latest entry of the type with the same description, digits ignored), else a default per type picked on the screen.

  10.4a done as: Core `CsvTable` (own delimiter choice = the one giving the largest table, because CsvHelper's detection
  picks "," in "-45.000,00;…" files; the longest run of rows with the common width, so title / totals lines don't
  break it; header-less files get "Column n"; strict UTF-8 else Latin-1), `StatementDate` (layouts + optional time;
  ambiguous day/month → the shortest date span, then the culture's order; the screen offers the choice),
  `StatementAmount` (per-column decimal separator; "(12.00)", "12.00-", currency signs/codes; a column qualifies with
  all but one or 20% of its values readable), `StatementMapping.Guess` (en/es header names, else the values),
  `IStatementImportService` (preview with counted duplicates per account: day + type + cents + description; import
  in one DB transaction; undo deletes the new IDs). App: Settings → Import bank statement → `ImportStatementPage`
  (pickers for account / columns / date format, "positive amounts are expenses" switch, default categories,
  preview rows with include box and tap-to-change category, pinned Import button, then Undo / OK). CsvHelper
  33.1.0 under Apache-2.0 on the About page and in THIRD-PARTY-NOTICES.md.
  Review: `Guess` returns a `StatementGuess` (mapping + date layouts per date column + amount columns) computed once off
  the UI thread; the VM keeps one mapping updated by each picker.

  10.4b done as: Core `StatementMappingStore` (preferences JSON, last 20 layouts): after an import it saves the columns,
  date pattern, "positive amounts are expenses" and the account under the file's layout = normalized header names
  (`CsvTable.HasHeader`), or the column count for a header-less file ("Column n" depends on the language). A file with a
  known layout opens with those choices if they still fit it (date column reads with the saved pattern, amount column
  is still one), and a note says so; changing a column hides the note. Not saved: the default categories (learned
  from history anyway). OFX/QIF stays for later.

### 10.5 Backups off the device
- Scheduled automatic local backups (weekly, keep last N).
- Optional cloud: Google Drive appDataFolder (Google.Apis.Drive.v3, Apache-2.0) or WebDAV/Nextcloud
  (WebDav.Client, MIT). Encrypted zip. (Replaces Stage 5.2 cloud sync for v1; real two-way sync stays out of scope.)

  Split by agreement (2026-10-16): **10.5a** = scheduled backups to a folder the user picks, **10.5b** = optional
  password (AES zip) for manual and automatic backups, asked on restore. Owner choices: the system folder picker (any
  provider: phone storage, SD card, Google Drive / Nextcloud apps) instead of the Drive API or WebDAV (no OAuth, no new
  dependency); optional password; daily / weekly / monthly, keep N.

  10.5a done as: Core `IBackupFolder` (Android `AndroidBackupFolder` = Storage Access Framework tree with a persisted
  permission, `MainActivity.StartForResultAsync`; `NoBackupFolder` elsewhere, card hidden), `AutoBackupSettings`
  (off, weekly, keep 5 by default), `AutoBackupService` (`RunIfDueAsync` from the daily job and at app start after
  recurring processing; due by whole days since the last backup; skipped while the data's fingerprint
  (`IDataExportService.GetFingerprintAsync`: SHA-256 of the rows, not the export time) is unchanged and a backup is
  still in the folder; written in the app folder first, then copied; keeps the newest N `MoneyRecord_AutoBackup_*.zip`
  and never touches other files; failures logged and remembered), Core `AutoBackupViewModel` (turning on asks for a
  folder and backs up at once; "can't reach the folder" when access was lost, Back up now disabled then). Restore is
  unchanged: an automatic backup is restored with Select Backup File.

### 10.6 Savings goals
- Goal = target amount + date + linked account(s); progress from account balance; projection ("on track by …").

### 10.7 Split transactions and payees
- One payment across several categories (`TransactionSplit` table); reports count splits by category.
- Optional payee field with autocomplete.

### 10.8 Debts and loans
- Track money lent/borrowed with a person; partial repayments; reminders.

### 10.9 Home-screen widget (Stage 5.5)
- Balance widget + quick-add expense widget (Android `AppWidgetProvider` via platform code).

### 10.10 Receipt OCR
- Plugin.Maui.OCR (confirm licence on the repo first) to pre-fill amount/date from a receipt photo.

### 10.11 PDF / CSV report export
- Monthly report PDF with PDFsharp/MigraDoc (MIT); not QuestPDF (licence).

## Licence rule for every new dependency
MIT, BSD, Apache-2.0 or LGPL only. No GPL-2.0-only, MS-PL-only, or "community"/non-commercial licences.
Add each one to `THIRD-PARTY-NOTICES.md`.

## Verify
Each feature: unit tests in Core + a `/run-moneyrecord` screenshot flow (en + es) added to its PR description.
