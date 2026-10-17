# Stage 12: Travel Mode

**Status**: Done (12.1, 12.2; 12.2 in review)
**Depends on**: Stages 6–11
**Source**: User request, 2026-10-14; designed with the owner 2026-10-17

## Goal
Record a trip's spending apart from everyday finances: its own budget, its own categories, its own list. When the trip
ends, its expenses move into the normal ledger under real categories, so reports keep making sense afterwards.

## Decisions (owner, 2026-10-17)
- Trip expenses leave their account right away (balances always right). While the trip is active their ledger category is
  a system **"Travel"** category (`Category.IsTravel`, created on first use like the debt Interest category), so Home,
  reports and budgets show the trip as one Travel slice.
- Each trip expense also has a **trip category** (Lodging, Flights, Transport, Food, Activities, Shopping, Other): one
  shared, editable list (`TripCategory`, seeded in the app language); it drives the trip's own breakdown and limits.
- **End trip**: one row per trip category used, choose the real expense category for each (prefilled from last time via
  `TripCategory.LastCategoryId`, plus "same for all"); the expenses move in one DB transaction. Expenses keep `TripId` +
  `TripCategoryId`, so ended trips stay readable. A trip can be **reopened** (expenses back to Travel).
- One active trip at a time; expenses in any currency; the trip summary converts into the trip currency with the
  downloaded rates (`CurrencyConverter`), what has no rate is listed apart (never added).
- A trip: name, dates (end optional), currency, optional budget, default account, optional limit per trip category.
- Add form: while a trip is active a new expense goes to it by default (banner + trip category grid); "Not part of the
  trip" brings back the normal categories. Income, transfers, splits and schedules stay normal.

## 12.1 (merged, PR #61)
- Core: `Trip`, `TripCategory`, `TripBudget`, `Transaction.TripId/TripCategoryId`, `Category.IsTravel`, `TripService`
  (start, summary, end, reopen, delete, trip categories), backup (still v4) + CSV columns `Trip,TripCategory`, tests.
- App: More → Trips (`TripsPage` list + start/edit sheet, `TripPage` summary / per-category / expenses / edit / delete,
  `TripCategoriesPage`), add-form trip mode, a trip line in the transaction details, Travel never offered in pickers
  (add form, CSV import) and not deletable.

## 12.2
- `TripPage`: End trip sheet (one row per trip category used, prefilled from the last end, "same category for all"), Reopen for an ended trip; Home card for the active trip (name, spent of budget; a tap opens it).

## Verify
Core tests (TripServiceTests, backup round-trip) + a `/run-moneyrecord` flow (en + es): start a trip, add expenses, check
Home / Transactions show the Travel slice, end it with a mapping, reopen, backup → restore.
