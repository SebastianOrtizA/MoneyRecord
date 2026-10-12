# Stage 8: UX Overhaul

**Status**: In progress (8.1 done; 8.2 in review)
**Depends on**: Stage 7 (LedgerEntry, IDialogService, single report VM)
**Complexity**: High (navigation and several screens change)
**Source**: Product & engineering review, 2026-10-11 (P6, P7, P9, N5–N8) + device check screenshots

## Goal
A clearer, faster app: fewer places to look, faster entry, reports people can act on.

## Tasks

### 8.1 Navigation: bottom tabs
- Replace the 10-item flyout with Shell `TabBar`: **Home · Transactions · Reports · Budgets · More**.
- More = Accounts, Categories (income/expense tabs), Recurring, Transfers, Settings.
- Remove the floating action menu (`Controls/FloatingActionMenu`, FabComponents) or keep exactly one add entry
  point: a single FAB that opens a bottom sheet (Expense / Income / Transfer). Removes the overlap with Edit and sort
  buttons (N5) and the duplicate add buttons (N6).
- Every tab and More item has a distinct icon (MDI `FontImageSource`).
- Done as a Shell `TabBar` with implicit tab routes, so tab pages stay one-segment roots (`//MainPage`, `//MorePage`)
  and `NavigationHistory` needed no logic change. Reports is one tab with Expenses / Incomes / Trends as top tabs until
  the hub (8.7). More (`MorePage` + `MoreViewModel`) pushes Accounts, Categories, Recurring, Transfers and Settings as
  registered routes, so back returns to More. Categories is one page with an Income / Expense switch
  (`SelectCategoryTypeCommand`) instead of two Shell routes. The draggable `FloatingActionMenu` + `FabComponents` and
  Home's three round add buttons are gone; `Controls/AddEntryButton` is a fixed FAB that opens an Expense / Income /
  Transfer bottom sheet (in-page overlay, slides up; Android back closes it first via `AppShell`), on Home and
  Transactions only. Add pages hide the tab bar. Android `dimens.xml` keeps the active tab label at 12sp so
  "Transacciones" / "Presupuestos" aren't cut off.
  **Deviation:** the full list (search, filters, grouping, sort, edit/delete) moved from Home to a new
  `TransactionsPage` + `TransactionsViewModel` now, so the Transactions tab isn't empty; that is the first bullet of
  8.3 (chips, swipe, detail sheet and empty state stay in 8.3). Home lost the list and the quick add buttons;
  `MainViewModel` is down to the cards, Recent and budgets. The Recent card's "See all" link is still 8.2.

### 8.2 Home
- Balance card shows one subtotal per currency (from Stage 6.5). Tapping a currency row opens the Accounts screen
  filtered to that currency; tapping the card header opens all accounts (not an alert).
- Income / Expense / Net cards; Net uses sign + colour.
- Budget summary uses `IBudgetSummaryService` with readable colours and a "View all" link.
- Recent 5 entries + "See all" → Transactions tab.
- Clear-filters button appears only when a filter is active (N7).
- Done as: the balance card opens Manage Accounts (the old balances alert and its strings are gone); with several
  currencies each line opens the accounts in that currency (`ManageAccountsPage?CurrencyCode=EUR`, a "EUR accounts ·
  Show all" chip on the page). `CurrencyBalance.DisplayLines` returns `BalanceLine(Text, CurrencyCode)`. Net uses
  `MoneyFormatter.FormatSigned` (+$85.00) via `MoneyConverter` `ConverterParameter=Signed`. Recent has "See all" →
  `//TransactionsPage`, Budget summary "View all" → `//BudgetPage` (`CardLink` style), and its rows show "spent /
  limit". Dark-mode amounts on the Income / Expense / Net cards are near-white (red on red was unreadable). N7: the
  filter toggle used the filter-remove icon, so it looked like a clear button; it is a plain filter icon now and the
  Clear button only shows with an active filter.

### 8.3 Transactions tab
- Full list moved off Home: search, filter chips (type, category, account, amount, tags), group by none/category/account.
- Swipe to edit/delete; tap opens a detail sheet (with receipt preview).
- Proper localized empty state.

### 8.4 Fast entry (P7)
- Field order: Amount (autofocus, numeric keypad) → Category (icon grid, recent first) → Account → Date → optional
  Description/Tags/Receipt/Recurring under "More details".
- Description optional; defaults to the category name.
- Remember last used account and category per type (`IPreferencesService`).
- Save button pinned at the bottom, always visible.

### 8.5 Accounts screen
- Cards with icon, name, currency badge, balance in its own currency, last activity.
- Grouped by currency (base first), each group with a subtotal header in that currency ("EUR · €300.00 · 2 accounts").
  Accepts a currency filter from Home.
- Add/Edit in a bottom sheet instead of the always-open form on top of the list.

### 8.6 Budgets
- Edit in a bottom sheet (period, limit) instead of `DisplayPromptAsync`.
- Status colours: green / amber / red with text labels ("On track", "Near limit", "Over") so colour isn't the only signal.
- Empty state with a budget/target icon instead of "#" (N8).

### 8.7 Reports hub (P9) — LiveChartsCore
- Add `LiveChartsCore.SkiaSharpView.Maui` (MIT, 2.0.5); retire `Controls/ChartDrawables.cs`.
- Tabs: **Spending** (donut + ranked list) · **Income** · **Trends** (monthly bars) · **Cash flow** (income vs
  expense line) · **Net worth** (sum of account balances over time).
- Period picker shared across tabs; "vs previous period" delta on every headline number.
- Tap a slice/bar → Transactions tab pre-filtered to that category/month.
- Budget vs actual per month for budgeted categories.

### 8.8 First-run setup
- 3 steps: language & base currency → starting accounts with balances (Cash pre-filled) → pick default categories.
- Skippable; reachable later from Settings.

### 8.9 Settings
- Language picker (System / English / Español) using `LocalizationService.SetCulture`.
- Base currency, theme (System/Light/Dark), backup section, about/licences (list third-party licences, required for
  AGPL distribution with MIT/Apache deps).

## Tests
- ViewModel tests for entry defaults (remembered account/category, description fallback).
- Report aggregation tests in Core (period deltas, net worth series).

## Verify on device (`/run-moneyrecord`)
Screenshot each tab in light, dark and Spanish; check that nothing is covered by the FAB, every icon renders, and
an expense can be added in ≤ 4 taps after amount entry.

## Done when
Bottom tabs ship, the flyout and duplicate add buttons are gone, entry is description-optional with remembered
defaults, and the Reports hub runs on LiveChartsCore with drill-down.
