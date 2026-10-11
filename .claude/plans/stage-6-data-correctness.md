# Stage 6: Data Correctness & Trust

**Status**: In progress — 6.0–6.4 done (2026-10-11)
**Depends on**: Stage 4 (complete). Blocks Stages 7–10.
**Complexity**: High (schema migration of user data)
**Source**: Product & engineering review, 2026-10-11 (P1–P5, P8, P12, N1–N4)

## Goal
Users can trust every number the app shows and never lose data. Fix the money format and schema
foundation first, because everything later builds on them.

## Tasks

### 6.0 Quick wins (ship first, independent of the migration) — ✅ done
- **N1 — first expense blocked**: seed Cash with `AllowNegativeBalance = true` in
  `DatabaseInitializer.AddDefaultAccountAsync`, and set the seeded Cash `CurrencyCode` to the device currency
  (it is empty today). Existing installs are left alone: those users have already worked around it.
- **N2 — "¤" currency sign**: `LocalizationService.SetCulture` must keep the device region
  (`es` + device `CO` → `es-CO`, never plain `es`). Use `CultureInfo.CurrentCulture.Name` when its language
  matches; otherwise fall back to `en-US` / `es-ES`.
- **N3 — negative amounts**: new `MoneyConverter` that formats with the sign before the symbol (`-$50.00`) and a
  `AmountColorConverter` (negative → expense red). Apply it to Net on `MainPage.xaml:164`.
- **N4 — receipt icons**: `AddTransactionPage.xaml:61,71`: camera `F0100`, gallery `F02E9`.
- **P6 — menu icons**: add `home`, `wallet`, `swap`, `folder`, `settings`, `repeat` SVGs to `Resources/Images`
  (only `chart.svg` exists) or switch the flyout to `FontImageSource` with MDI glyphs. Give every item a distinct icon.
- Done: also fixed the Home "receipt attached" glyph (was book-music `F0067` → `F0449`), the Home balance
  and income/expense cards now use `MoneyConverter`, and the unused `chart.svg` was removed. Flyout uses
  `FontImageSource` glyphs. Still open for 6.6: "Your Accounts" / "Initial:" untranslated, "octubre 11, 2026" date pattern.

### 6.1 Schema migration runner — ✅ done
- New `Services/Repositories/Migrations/` with `IMigration { int Version; Task UpAsync(SQLiteConnection) }`.
- `DatabaseInitializer` reads `PRAGMA user_version`, runs pending migrations in one transaction each,
  then sets `user_version`. Current schema = version 1 (baseline, no-op).
- Back up `moneyrecord.db3` to `moneyrecord.db3.bak-v{n}` before any migration that rewrites data.
- Add indexes (done via `[Indexed]` on the models, see below): `Transaction(Date)`, `Transaction(AccountId)`, `Transaction(CategoryId)`, `Transfer(Date)`,
  `Transfer(SourceAccountId)`, `Transfer(DestinationAccountId)`.
- Done as `MigrationRunner` + `M001_Baseline`. **Contract: models describe the latest schema.** A new database is created
  from the models and stamped with the latest version (migrations never run on it); a migration only transforms a database
  from an older app version, after `CreateTableAsync` has added new columns/indexes. Indexes are `[Indexed(Name = "IX_…")]`
  on the models, not a migration. `IMigration.Up` is sync (`void Up(SQLiteConnection)`); `user_version` is set in the same
  transaction. Backups use `VACUUM INTO`, only for `RewritesData = true`. A DB newer than the app throws. New migrations:
  add to `MigrationRunner.All`.

### 6.2 Money as integer minor units (v2 migration) — ✅ done
- sqlite-net stores `decimal` as REAL → rounding drift. Add `long AmountMinor` (and `InitialBalanceMinor`,
  `LimitAmountMinor`) columns; migrate with `ROUND(Amount * 100)`; keep `decimal Amount` as an `[Ignore]`
  property computed from the minor value so ViewModels don't change.
- Minor-unit scale per currency (JPY/CLP/KRW = 0 decimals) via `CurrencyService.GetMinorUnits(code)`.
- Tables: Transaction, Transfer, Account, Budget, RecurringTransaction.
- Backup JSON format bumps to `Version = 2`; import accepts v1 (decimal) and v2 (minor).
- Done as `M002_MoneyAsCents` + `Helpers/Money`. **Deviation: fixed scale of 100 (cents) for every currency**, columns
  `AmountCents` / `InitialBalanceCents` / `LimitAmountCents`. Rows have no currency of their own (it comes from the account),
  so a per-currency scale would make every model depend on its account and would rescale amounts when an account's currency
  changes. JPY stores multiples of 100; zero-decimal display/input rounding (`GetMinorUnits`) moves to 6.5.
  The migration skips tables that an old install never had (no legacy column). Backup JSON v2 carries both the decimal and
  the cents property; cents is written later in the file, so it wins on import, and v1 files import through the decimal setter.
  The migration converts in C# through `(decimal)double` (how the app used to read REAL), not SQL `ROUND`, so a stored
  1.0049999999999999 keeps the $1.01 the user saw. Input above `Money.MaxAmount` (just under 1e12) is rejected.
- Follow-up (once v2 has been out for a few releases): a migration that drops the unused REAL columns
  (`ALTER TABLE … DROP COLUMN`, SQLite 3.35+).

### 6.3 Delete cascades (transactional) — ✅ done
- Account delete (`ManageAccountsViewModel.DeleteAccountAsync`): in one `RunInTransactionAsync`, reassign
  transactions **and** transfers (or block deleting when transfers exist, and say why) **and** recurring rules,
  then delete. Move this into a new `IAccountService.DeleteAsync(accountId)`.
- Category delete: reassign transactions, budgets (merge or delete with confirmation), and recurring rules in one
  transaction → `ICategoryService.DeleteAsync(categoryId, replacementId)`.
- Transaction delete removes its receipt file.
- Done as `AccountService` / `CategoryService` (`GetUsageAsync` + `DeleteAsync`, one `RunInTransactionAsync` each).
  Accounts with transfers are refused (moving a transfer to the default account could make it point at itself); the
  user deletes those transfers first. Budgets move to the replacement category unless it already has one (then they are
  deleted). Receipt files are only deleted from inside `receipts/` (`ReceiptFiles`). `DatabaseInitializer(path)` exists
  for integration tests.

### 6.4 Safe backup & restore — ✅ done
- Restore inside a single transaction (delete + insert), or restore into a temp DB file and swap.
- Automatic snapshot (`Backups/pre-restore-{timestamp}.json`) before every restore, with a "Undo restore" option.
- Backup becomes a `.zip`: `data.json` + `receipts/`. Store receipt paths relative (`receipts/receipt_x.jpg`).
- Map category IDs by old ID, not by name+type (the current name match breaks on duplicates).
- (From 6.3 review) Removing or replacing a receipt photo on an existing transaction leaves the old file in `receipts/`;
  delete it on save with `ReceiptFiles.Delete` (and the unsaved new photo on cancel).
- Done as `IDataExportService.ExportBackupAsync(path)` / `RestoreAsync(path)` (backup format v3). The backup is read and
  validated in full (newer format, duplicate IDs, missing data.json) before anything changes; receipts are unpacked to
  `receipts.restoring` and swapped in only after the single `RunInTransactionAsync` (delete + insert) commits; account and
  category caches are invalidated. **Deviations:** rows keep their **original IDs** (`Insert(row, "OR REPLACE")` into the
  emptied tables) instead of an old→new ID map, so every reference (category, account, transfer) survives as-is; the
  snapshot is a full `.zip` (`Backups/pre-restore-{timestamp}.zip`, last 5 kept, listed with the other backups), not
  `.json`, so undo brings receipts back too. Undo = restoring the snapshot, offered in the "Restore complete" dialog.
  Old `.json` backups (v1/v2) still restore; their receipts are kept only if the file is already in `receipts/`.
  Paths are relative only inside the zip; the database still stores absolute receipt paths (iOS changes the app
  folder on reinstall: revisit with Stage 7). Unsaved photos are discarded on Cancel, on leaving the page
  (`OnNavigatedFrom`) and when replaced; receipt file names now include milliseconds and are written with `File.Create`.

### 6.5 Currency display everywhere (P1, P2)
- Remove every hard-coded `'${0:N2}'` (MainPage, BudgetPage, report pages, TransfersPage, RecurringTransactionsPage,
  `ChartDrawables` axis, `MainViewModel.ShowAccountBalancesAsync`, report detail popups) → `MoneyConverter`
  with the account currency, else the device culture.
- Totals across accounts: never add different currencies together. The Home balance card shows **one subtotal per
  currency**, each in its own currency, base currency first (Settings → base currency, default = device region currency):
  ```
  $1,250.00 USD   (base)
  €300.00 EUR
  COP 450.000
  ```
  With a single currency, the card looks as it does today. New `IBalanceService.GetBalancesByCurrencyAsync()` returns
  `(CurrencyCode, BalanceMinor, AccountCount)` rows; accounts with an empty `CurrencyCode` count as the base currency.
- `CurrencyMaskBehavior` shows the selected account's symbol, not `$`.
- (From 6.2) `CurrencyService.GetMinorUnits(code)`: JPY/CLP/KRW show and accept 0 decimals; storage stays in cents.
- (From 6.3) Deleting an account moves its transactions to the default account even when the currencies differ; refuse
  or ask once accounts can have different currencies. Manage Accounts shows negative balances in green.

### 6.6 Localization cleanup (P8)
- Move the ~26 hard-coded strings ("No activity", "Transfers", "Transfer to/from", "Unknown", "Cash",
  "1 transactions" plural, "All/Income/Expense/Transfer") into both `.resx` files.
- Filters use enums/IDs, not display strings (`MainViewModel.ApplyFilters`).
- Dates: use culture patterns (`D`, `MMM d`) instead of `"MMM dd, yyyy"`; check "11 de octubre de 2026" in Spanish.
- Budget colours (P12): replace `Colors.Yellow` with an accessible amber (`#B26A00` light / `#F2B65C` dark) and
  never use the progress colour for the amount text.

### 6.7 Housekeeping
- Replace FluentAssertions 8.* (Xceed licence, non-commercial only) with AwesomeAssertions (Apache-2.0); pin all test
  package versions.
- `.gitignore`: add `.codegraph/`; remove committed `AppResources.resources` if it's a build artifact.

## Tests
- `GetBalancesByCurrencyAsync`: USD + EUR + COP accounts → three rows, none summed across currencies; empty code → base.
- Migration tests against a real temp SQLite file: v1 DB with decimals → v2, sums equal to the cent.
- Cascade tests: delete account with transfers/recurring; delete category with budget/recurring.
- Restore round-trip incl. receipts; restore failure midway leaves the original data intact.
- `MoneyConverter` / culture tests for en-US, es-CO, es-ES, ja-JP (0 decimals).

## Verify on device (`/run-moneyrecord`)
`fresh` → add a 50 expense on Cash without errors (N1) → add a EUR account with 100 → Home shows a USD row and a
€100.00 EUR row, not a combined total → accounts show `€`/`$`, never `¤` (N2) → Net shows `-$50.00`
in red (N3) → receipt buttons show camera/gallery (N4) → menu items all have icons → `locale es-ES` shows Spanish dates and
"1 transacción".

## Done when
All P1–P5, P8, P12, N1–N4 items are fixed, migrations are covered by tests, and the device check passes.
