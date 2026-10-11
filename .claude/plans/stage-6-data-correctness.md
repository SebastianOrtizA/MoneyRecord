# Stage 6: Data Correctness & Trust

**Status**: Pending
**Depends on**: Stage 4 (complete). Blocks Stages 7–10.
**Complexity**: High (schema migration of user data)
**Source**: Product & engineering review, 2026-10-11 (P1–P5, P8, P12, N1–N4)

## Goal
Users can trust every number the app shows and never lose data. Fix the money format and schema
foundation first, because everything later builds on them.

## Tasks

### 6.0 Quick wins (ship first, independent of the migration)
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

### 6.1 Schema migration runner
- New `Services/Repositories/Migrations/` with `IMigration { int Version; Task UpAsync(SQLiteConnection) }`.
- `DatabaseInitializer` reads `PRAGMA user_version`, runs pending migrations in one transaction each,
  then sets `user_version`. Current schema = version 1 (baseline, no-op).
- Back up `moneyrecord.db3` to `moneyrecord.db3.bak-v{n}` before any migration that rewrites data.
- Add indexes in v2: `Transaction(Date)`, `Transaction(AccountId)`, `Transaction(CategoryId)`, `Transfer(Date)`,
  `Transfer(SourceAccountId)`, `Transfer(DestinationAccountId)`.

### 6.2 Money as integer minor units (v3 migration)
- sqlite-net stores `decimal` as REAL → rounding drift. Add `long AmountMinor` (and `InitialBalanceMinor`,
  `LimitAmountMinor`) columns; migrate with `ROUND(Amount * 100)`; keep `decimal Amount` as an `[Ignore]`
  property computed from the minor value so ViewModels don't change.
- Minor-unit scale per currency (JPY/CLP/KRW = 0 decimals) via `CurrencyService.GetMinorUnits(code)`.
- Tables: Transaction, Transfer, Account, Budget, RecurringTransaction.
- Backup JSON format bumps to `Version = 2`; import accepts v1 (decimal) and v2 (minor).

### 6.3 Delete cascades (transactional)
- Account delete (`ManageAccountsViewModel.DeleteAccountAsync`): in one `RunInTransactionAsync`, reassign
  transactions **and** transfers (or block deleting when transfers exist, and say why) **and** recurring rules,
  then delete. Move this into a new `IAccountService.DeleteAsync(accountId)`.
- Category delete: reassign transactions, budgets (merge or delete with confirmation), and recurring rules in one
  transaction → `ICategoryService.DeleteAsync(categoryId, replacementId)`.
- Transaction delete removes its receipt file.

### 6.4 Safe backup & restore
- Restore inside a single transaction (delete + insert), or restore into a temp DB file and swap.
- Automatic snapshot (`Backups/pre-restore-{timestamp}.json`) before every restore, with a "Undo restore" option.
- Backup becomes a `.zip`: `data.json` + `receipts/`. Store receipt paths relative (`receipts/receipt_x.jpg`).
- Map category IDs by old ID, not by name+type (the current name match breaks on duplicates).

### 6.5 Currency display everywhere (P1, P2)
- Remove every hard-coded `'${0:N2}'` (MainPage, BudgetPage, report pages, TransfersPage, RecurringTransactionsPage,
  `ChartDrawables` axis, `MainViewModel.ShowAccountBalancesAsync`, report detail popups) → `MoneyConverter`
  with the account currency, else the device culture.
- Totals across accounts: until Stage 10 adds exchange rates, show the total only for accounts in the **base currency**
  (Settings → base currency, default = device region currency) and list other-currency balances separately
  ("+ €50.00 in other currencies"). Never add different currencies together.
- `CurrencyMaskBehavior` shows the selected account's symbol, not `$`.

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
- Migration tests against a real temp SQLite file: v1 DB with decimals → v3, sums equal to the cent.
- Cascade tests: delete account with transfers/recurring; delete category with budget/recurring.
- Restore round-trip incl. receipts; restore failure midway leaves the original data intact.
- `MoneyConverter` / culture tests for en-US, es-CO, es-ES, ja-JP (0 decimals).

## Verify on device (`/run-moneyrecord`)
`fresh` → add a 50 expense on Cash without errors (N1) → accounts show `€`/`$`, never `¤` (N2) → Net shows `-$50.00`
in red (N3) → receipt buttons show camera/gallery (N4) → menu items all have icons → `locale es-ES` shows Spanish dates and
"1 transacción".

## Done when
All P1–P5, P8, P12, N1–N4 items are fixed, migrations are covered by tests, and the device check passes.
