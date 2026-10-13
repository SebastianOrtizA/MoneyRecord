# Stage 11: End Review

**Status**: Collecting (fix after the other stages, or earlier when a stage already works in the area)
**Source**: Issues and small requests the user noted while reviewing Stages 8.7a–8.7b (2026-10-13)

## Tasks

### 11.1 Currency symbol and code said twice
- The Home balance card shows "COL$6,000,300.00 COP": the symbol prefix and the trailing code say the same thing.
  Likely for every prefixed symbol (US$, CA$, MX$, A$…). It comes from `CurrencyBalance.DisplayLines`, which appends
  the code after `MoneyFormatter.Format` when there are several currencies.
- Pick one form per line (code only, or a plain symbol + code) and check the other places that put a code next to a
  formatted amount.

### 11.2 More tab reopens the last pushed page
- More → open e.g. Accounts → switch to Home or Transactions → tap More again: it shows Accounts, not the More menu.
  Shell keeps each tab's stack; re-selecting More should pop to `//MorePage` (tab re-selection / `Shell.Navigating`),
  keeping `NavigationHistory` consistent.

### 11.3 Optional cents per account
- Let the user choose per account whether amounts show cents ("COL$6,000,300" instead of "COL$6,000,300.00").
  Storage stays in cents (fixed scale 100); this only changes display and input, as zero-decimal currencies already
  do (`CurrencyService.GetMinorUnits`).
- Idea: an `Account.ShowCents` flag (migration + backup format) feeding the minor units `MoneyFormatter` /
  `CurrencyMaskBehavior` use for that account's amounts. Decide how totals that mix accounts are shown.

### 11.4 Editing a category low in the list shows nothing
- Categories: scroll down, tap Edit on a category near the bottom → nothing seems to happen. The edit form is the
  inline card at the top of the page's `ScrollView` (`IsEditMode` swaps it in for the add card), so it opens off
  screen and the user has to scroll back up.
- Requested: after Edit, the page scrolls to the top so the form is visible. Alternative to weigh: open the edit
  (and add) form in the shared `Controls/BottomSheet`, like Accounts (8.5) and Budgets (8.6), so it never depends on
  the scroll position.

## Done when
Each item is fixed or consciously dropped with the user.
