# Stage 11: End Review

**Status**: In progress (11.1 + 11.2 in review)
**Source**: Issues and small requests the user noted while reviewing Stages 8.7a–8.7b (2026-10-13)

## Decisions (owner, 2026-10-16)
- 11.1 (refined on the device, 2026-10-17): plain symbols only (never COL$), always in front of the number in every
  language ("$1.200,00", "€0,00"; "1.401,75 $" looked wrong in Spanish), and the code at the end only when needed:
  lines that list several currencies, or a non-base currency whose symbol is shared ("$1.500,00 USD" while the base
  is EUR). Applies to every amount in the app, not only the balance card.
- 11.3: the cents choice is per currency, not per account (totals are per currency, so they never mix forms).
- 11.4: add and edit move to the shared `BottomSheet`.
- PRs: 11.1 + 11.2, then 11.3, then 11.4 + 11.6, then 11.5.

## Tasks

### 11.1 Currency symbol and code said twice
- The Home balance card shows "COL$6,000,300.00 COP": the symbol prefix and the trailing code say the same thing.
  Likely for every prefixed symbol (US$, CA$, MX$, A$…). It comes from `CurrencyBalance.DisplayLines`, which appends
  the code after `MoneyFormatter.Format` when there are several currencies.
- Pick one form per line (code only, or a plain symbol + code) and check the other places that put a code next to a
  formatted amount.
- Done as: `Currency.Symbol` is the plain sign ("$" for COP, CAD, MXN...). `MoneyFormatter.Format` puts it in front
  in every culture and adds the code when `CurrencyService.HasSharedSymbol` and the currency isn't the base;
  `FormatWithCode` always adds it (Home balance lines, Accounts group header "$1.401,75 USD · 1 cuenta");
  `FormatBesideCode` never does (account cards next to their chip, amount fields). The per-culture symbol lookup is gone.

### 11.2 More tab reopens the last pushed page
- More → open e.g. Accounts → switch to Home or Transactions → tap More again: it shows Accounts, not the More menu.
  Shell keeps each tab's stack; re-selecting More should pop to `//MorePage` (tab re-selection / `Shell.Navigating`),
  keeping `NavigationHistory` consistent.
- Done as: `AppShell` pops the stack of the tab just left (`PopToRootAsync`) on every tab change, so every tab (not only
  More) opens at its own page; Home → Accounts → Transactions → Home shows Home. Re-tapping the tab already shown
  also returns to its page (Android: `TabReselectShellRenderer` adds the bottom bar's reselect listener).

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

### 11.5 `Frame` → `Border` sweep
- `Frame` is obsolete since .NET 9. Settings (and other older pages) still use it for cards, while newer pages
  (About, Accounts, Budgets) use `Border`. Replace the remaining `Frame`s with `Border` + a shared card style so
  every card looks the same (raised in the 8.9 review).

### 11.6 Small things seen in the store screenshots (9.6)
- Utilities and Home seed categories share the icon `F0D15` (`DefaultCategory`); give Utilities its own.
- The category grid cuts long names ("Entretenimient" in Spanish); wrap to two lines or shrink the font.
- Spanish report tabs read "GASTOS · INGRESO": make it "INGRESOS".
- README "Features" still describes the pre-Stage-8 app (flyout menu, floating button, report heatmaps); rewrite it
  from the store description.

## Done when
Each item is fixed or consciously dropped with the user.
