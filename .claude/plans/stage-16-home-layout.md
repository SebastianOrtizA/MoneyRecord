# Stage 16: Customizable Home (order, show/hide, Debts card)

**Status**: 16.1 in review (2026-10-25); 16.2 not started
**Depends on**: 10.8 debts
**Complexity**: Medium (2 PRs)
**Source**: User request, 2026-10-25

## Goal
The user chooses which Home cards show and in what order. A new **Debts** card is offered; it is hidden by default (10.8
chose "no Home card", so it stays opt-in).

## Decisions
- `HomeCard` enum in Core: Balance, PeriodSummary, Recent, Upcoming, Budgets, Trip, Goals, Debts. The layout is one
  preference string (for example `"Balance,PeriodSummary,Recent,…;hidden=Debts"`), read and written by `HomeLayoutSettings`
  (Core, `IPreferencesService`). Unknown names are dropped; cards missing from a saved layout (added in a later version) are
  appended with their default visibility. So the layout keeps working as new cards arrive (for example the Stage 18 one).
- A card shows when it is **enabled and has data** (today's `HasBudgets`, `HasActiveTrip`, ...). A hidden card skips its
  loads in `MainViewModel`, so Home also gets lighter.
- Edit screen: Settings → **Home screen** (new push route `HomeLayoutPage`), plus a "Customize" link at the bottom of Home.
  A reorderable `CollectionView` (`CanReorderItems`) with a switch per row and "Reset to default". Balance can be moved but
  not hidden (it is the app's main figure); everything else can be hidden.

## Sub-stages (branch + PR each)

### 16.1 Layout and editor
**Done as (16.1)**: as planned, but the editor reorders by drag and drop (Drag/DropGestureRecognizer on each row; long-press, drop on another row) with move up / down arrows as a fallback, not a `CollectionView`, and `HomeCard` has no `Debts` yet (16.2 adds it; saved layouts append new cards). The Period picker and summary are one card (`PeriodSummary`).

- Core: `HomeCard`, `HomeLayoutSettings` (`Parse` / `Serialize` / `Default`) + `HomeLayoutTests` (round-trip, unknown names,
  new cards appended, Balance always shown).
- `MainPage.xaml`: each card `Border` gets `x:Name`; code-behind reorders the `VerticalStackLayout` children from
  `ViewModel.CardOrder` when it changes. The `Show…Card` view-model properties combine "enabled" with the existing `Has…`.
- `HomeLayoutPage` + `HomeLayoutViewModel` (MAUI project, transient DI), the Settings row and the Home link. Home re-reads
  the layout in `InitializeAsync`.

### 16.2 Debts card
- `MainViewModel` calls `IDebtService.GetAllAsync()` only when the card is enabled. The card shows "Owed to you" / "You owe"
  per currency (`CurrencyAmount.Totals`, never added together) and up to 3 open debts (person, outstanding, next due date).
  A tap opens `DebtsPage`. It is hidden by Hide balances, like the other amounts.
- Strings in en + es.

## Critical files
- New: `Models/HomeCard.cs`, `Services/HomeLayoutSettings.cs` + interface, `MoneyRecord/ViewModels/HomeLayoutViewModel.cs`,
  `Views/HomeLayoutPage.xaml`
- Changed: `MoneyRecord/ViewModels/MainViewModel.cs`, `Views/MainPage.xaml` + code-behind, `SettingsViewModel.cs` / page,
  `AppShell` (route), `MauiProgram.cs`, `AppResources*.resx`, `CLAUDE.md`
- Reused: `IPreferencesService`, `IDebtService`, `CurrencyAmount.Totals`, the `BottomSheet`-free push-page pattern

## Verify
- `dotnet test` (layout parsing and defaults).
- Android build + `/run-moneyrecord`, English and Spanish: reorder (Goals above Recent) and Home follows after Back; hide Budgets
  and it is gone with its load skipped; enable Debts and the card shows the emulator's debt; Reset restores the defaults.
