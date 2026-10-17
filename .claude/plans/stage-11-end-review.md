# Stage 11: End Review

**Status**: In progress (11.1–11.6 done; 11.7 in review)
**Source**: Issues and small requests the user noted while reviewing Stages 8.7a–8.7b (2026-10-13)

## Decisions (owner, 2026-10-16)
- 11.1 (refined on the device, 2026-10-17): plain symbols only (never COL$), always in front of the number in every
  language ("$1.200,00", "€0,00"; "1.401,75 $" looked wrong in Spanish), and the code at the end only when needed:
  lines that list several currencies, or a non-base currency whose symbol is shared ("$1.500,00 USD" while the base
  is EUR). Applies to every amount in the app, not only the balance card.
- 11.3: the cents choice is per currency, not per account (totals are per currency, so they never mix forms).
  Refined 2026-10-17: the switches live in Settings → Base currency ("Show cents", one per currency in use).
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
- Done as: per currency (owner decision), in Settings → Base currency → Show cents: one switch per currency in use (the
  base, then each account's; JPY/CLP/KRW have no cents and aren't listed), with a live sample. Saved in the
  `Currencies.HideCents` preference (like the base currency: not in backups), loaded by `CurrencyService.LoadSettings`.
  `CurrencyService.GetShownDecimals` feeds `MoneyFormatter` (lists, totals, cards round to whole units);
  `GetMinorUnits` stays the input precision, so typed cents are kept, and `MoneyFormatter.FormatForEntry` shows an
  amount's cents in a form field (a stored "$1.250,50" isn't rounded by opening and saving the form).

### 11.4 Editing a category low in the list shows nothing
- Categories: scroll down, tap Edit on a category near the bottom → nothing seems to happen. The edit form is the
  inline card at the top of the page's `ScrollView` (`IsEditMode` swaps it in for the add card), so it opens off
  screen and the user has to scroll back up.
- Requested: after Edit, the page scrolls to the top so the form is visible. Alternative to weigh: open the edit
  (and add) form in the shared `Controls/BottomSheet`, like Accounts (8.5) and Budgets (8.6), so it never depends on
  the scroll position.
- Done as: the page works like Accounts: the Income / Expense switch, one card per category (a tap opens the edit sheet),
  a + button for the add sheet, and Delete inside the edit sheet (its questions open over the sheet, so cancelling
  returns to it; Android back closes the sheet first). The inline add / edit cards and the per-row Edit / Delete buttons
  are gone. Also fixed: the default expense icon was `F09B5` (a pound weight, not in the picker, so nothing was
  highlighted); it is now Food (`F025A`).

### 11.5 `Frame` → `Border` sweep
- `Frame` is obsolete since .NET 9. Settings (and other older pages) still use it for cards, while newer pages
  (About, Accounts, Budgets) use `Border`. Replace the remaining `Frame`s with `Border` + a shared card style so
  every card looks the same (raised in the 8.9 review).
- Done as: no `Frame` is left. A `Card` style (Styles.xaml: white / #1E1E1E background, 1 px #E0E0E0 / #404040 stroke,
  radius 12, padding 16) is used by every white panel and list row (Home, Settings, About, Setup, Accounts, Categories,
  Budgets, Goals, Debts, Payees, Transactions, Recurring, Transfers, Import), which before mixed #3A3A3A and #404040
  dark strokes; pages keep their own Padding / Margin. `ChartCard` is based on it. Tinted cards (Home balance and
  period totals, the Recurring edit form and next-occurrence box) became plain Borders with their own colours. The
  Frames' Android shadow is gone: every card is flat with an outline, like the newer pages, except Home's balance card,
  which keeps a light `Shadow` (review).

### 11.6 Small things seen in the store screenshots (9.6)
- Utilities and Home seed categories share the icon `F0D15` (`DefaultCategory`); give Utilities its own.
- The category grid cuts long names ("Entretenimient" in Spanish); wrap to two lines or shrink the font.
- Spanish report tabs read "GASTOS · INGRESO": make it "INGRESOS".
- README "Features" still describes the pre-Stage-8 app (flyout menu, floating button, report heatmaps); rewrite it
  from the store description.
- Done as: Utilities has its own icon, lightning-bolt `F140B` (also offered in the icon picker; only new seeds get it,
  existing categories keep theirs). The category grid uses `AutoFitLabel` with `MaxLines="2"`: names wrap between
  words, and only the longest word has to fit, shrinking from 12 to 9 (a longer word still breaks). The grid never
  showed a second line: `FlexLayout` measures children at an unlimited width, so a wrapping `AutoFitLabel` now always
  measures as two lines at its largest font (rows are even). Report tab and title use the new `IncomeReport` key
  ("Income" / "Ingresos"; `Income` stays the singular entry type "Ingreso"), also in the PDF totals header. README
  Features rewritten for the current app (tabs, Reports, Currencies, CSV import, widget, setup).

### 11.7 Scheduled recurring transactions, future-dated entries (owner request, 2026-10-17)
- Recurring page: a + button (Expense / Income; no transfers) opens the add form in a "schedule" mode: the date is
  the first occurrence, tomorrow at the earliest, and saving stores only the schedule (`NextOccurrence` = that date,
  nothing processed yet). The daily processing creates each entry on its date, like any recurring transaction.
- Add form: "Make recurring" with a future date saves only the schedule too (option a); nothing counts before its date.
- Balances count only entries dated up to today: a future-dated transaction or transfer is listed, but no balance,
  total balance, widget, goal or net worth includes it before its date. What works back from today's balances (net
  worth, the PDF's closing balances, a goal's pace) stops at today too. Period totals (Home cards, reports, budgets)
  still count a period's entries by their dates.
- Recurring edit: while a schedule has created nothing yet, its first date can be moved (tomorrow at the earliest).
- Decisions (owner): reuse the add form over a new sheet; earliest date tomorrow; Expense / Income only.
- Done as: `BalanceService.CountedBefore` (the start of tomorrow, from `TimeProvider`) bounds every balance sum
  (`SumCentsByAccountAsync` / `SumCentsBySource|DestinationAsync` take `before`); `GetChangesSinceAsync` stops there too,
  so the PDF's closing balances and a goal's pace still add up, and Net worth loads movements only up to today.
  `RecurringStart` (Core) decides how a new schedule starts: today or earlier records the first entry now (as before);
  a future date saves only the schedule (`NextOccurrence` = that date, `LastProcessedDate` null) and the daily
  processing creates it on the day. The Recurring page's + asks Expense / Income and opens `AddTransactionPage` with
  `Schedule=true`: "First date" from tomorrow, recurrence options open, no split, receipt scan, tags, receipt or own
  conversion. A note under the date says what a future date does ("Only the schedule is saved: …" / "Dated in the
  future: it counts in balances from …"). The Recurring edit card shows "First date" while a (non-debt) schedule has
  created nothing. Also fixed: the add form's Monthly default never showed "Specific day of month" until the
  frequency was changed. Not changed: future-dated debt movements still count in a debt's outstanding amount (Debts
  page), and the Recurring edit form is still the inline card (a bottom sheet could follow, like 11.4).

## Done when
Each item is fixed or consciously dropped with the user.
