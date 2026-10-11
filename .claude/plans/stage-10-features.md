# Stage 10: New Features

**Status**: Pending
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

### 10.2 App lock and encryption (P11)
- Biometric/PIN lock on open and after N minutes in background (Oscore.Maui.Biometric, MIT. Check its NOTICE,
  it's derived from MS-PL code; fallback: direct AndroidX.Biometric binding).
- Encrypt the DB with SQLCipher (`SQLitePCLRaw.bundle_e_sqlcipher`); key in Android Keystore via `SecureStorage`.
  Migration: export → re-create encrypted DB → import; keep a pre-migration backup.
- Hide balances toggle (privacy mode) on Home.

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

### 10.4 Import from bank statements
- CSV import with CsvHelper (use its Apache-2.0 licence): pick file → map columns (date, description, amount,
  type/sign) → preview → duplicate detection (date + amount + description) → import into one account.
- Save mappings per bank. Later: OFX/QIF.

### 10.5 Backups off the device
- Scheduled automatic local backups (weekly, keep last N).
- Optional cloud: Google Drive appDataFolder (Google.Apis.Drive.v3, Apache-2.0) or WebDAV/Nextcloud
  (WebDav.Client, MIT). Encrypted zip. (Replaces Stage 5.2 cloud sync for v1; real two-way sync stays out of scope.)

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
