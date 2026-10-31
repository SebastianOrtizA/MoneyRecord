# Stage 20b: Visual and interaction polish (release 1.0.0, rest may follow in 1.0.x / 1.1)

**Status:** proposed (UX/UI review of 2026-10-30, checked on the emulator the same day); not started. Each sub-stage starts only when the
owner asks, one branch + PR each. Sits between Stage 20 (trust and polish) and Stage 21 (fast capture).

## Context
A senior UX/UI review of the 0.21.0 Debug build (Pixel 7 emulator, API 36, light and dark, font scale 1.0 and 2.0) found the logic ahead of
the surface: the add-entry loop is the slowest screen, screens carry too much chrome, colour use is inconsistent, and a few things clip or
break at large fonts. No schema change anywhere in this stage. Every item below was seen on the device unless marked *verify*.

Overlaps, so nothing is built twice:
- **Stage 20.5** (accessibility) owns large-font fixes and TalkBack; this stage only adds the clipped-amount finding there (see the 20.5 edit).
- **Stage 21.1** (fast capture) loses the "form is slow" motivation: the add form redesign is 20b.2. 21.1 is otherwise unchanged and keeps
  its long-press on the add button (straight to an expense), Duplicate, payee-fills-amount and app shortcuts; 21.2 quick entries stay.
- **Stage 22.2** puts its "Safe to spend" line on the Home balance card designed in 20b.4.
- **Stage 23.6** (saved filters) builds on the Transactions filter sheet from 20b.3, not on the chip bar.

## Release cut (decided 2026-10-30)
- **In 1.0.0:** 20b.1 (system bar, colour tokens) and the clipped-amount fix in 20.5.
- **20b.2 (add form) targets 1.0.0 through a signed pre-release** (like `v0.9.0-rc1`) and one full closed-test cycle. **Fallback rule:** if a
  regression is found and not fixed within that cycle, 20b.2 moves to 1.1.0; 1.0.0 does not wait for it.
- **Should be:** 20b.3 (period stepper, Transactions). If it slips, ship 1.0.0 without it and take it as 1.0.x.
- **Can follow without blocking:** 20b.4, 20b.5.

## Owner decisions (2026-10-30)
1. **Income colour: keep blue.** `IncomeColor` is blue (`#1976D2`, Colors.xaml) but `TransactionTypeToColorConverter`,
   `GroupTypeToColorConverter` and `BalanceToColorConverter` hard-code green (`#2E7D32`); the Home Net tile is green. Blue income with red
   expenses is a colour-blind-safe pair and already the token. The converters read the tokens; green stays only for "status good" (budget on
   track, goal reached). Revisit only if testers read blue income as a link or a neutral colour.
2. **Add button: keep the Expense / Income / Transfer sheet.** A wrong type that goes unnoticed costs more in a money app than the one tap it
   saves, and the real speed gains of 20b.2 come from Save above the keyboard and the account / date chips. The form still gets the type
   switch (a wrong pick is one tap to fix), and each type is visibly different (see 20b.2). Long-press on the add button opens an expense
   directly, as Stage 21.1 already plans. No "last used type" memory (it makes the same button behave differently each time).
3. **Amount entry: fix the keyboard first.** Try `UseWindowSoftInputModeAdjust(Resize)` so Save stays above it. A custom in-app number pad
   (locale decimal separator, key states, haptics, TalkBack) only if the form is still cramped on a short phone after that fix.
4. **Release scope:** the cut above.

## Design

### 20b.1 Foundations (1 PR, App)
- **System bar.** Dark icons on the `#512BD4` bar are unreadable (confirmed in light and dark, on every Shell page; the setup wizard
  already has a normal bar). Set light icons on the purple: `StatusBar.SetStyle(StatusBarStyle.LightContent)` and `SetColor` from
  `CommunityToolkit.Maui.Core.Platform` (CommunityToolkit.Maui 15.0.1 is referenced; check the exact API). Apply at start and in
  `ThemeService` on theme change; `SetupPage` (outside Shell) keeps dark icons on white. No `StatusBar` code exists today, so find first
  where the bar colour is set (Shell / `Maui.SplashTheme`) and change that single place. Check edge-to-edge on API 35+ and a 24-28 device.
- **Colour tokens.** Add `TextSecondary` / `TextSecondaryDark` and a `SecondaryLabel` `Label` style in `Styles.xaml`; `#666666` / `#AAAAAA`
  appear 161 times in 32 XAML files. Convert the pages touched by 20b.2-20b.5 and the shared controls first, the rest as they are touched
  (no 32-file sweep). Add tonal containers for the Home tiles: `IncomeContainer`, `ExpenseContainer`, `NetContainer` plus dark variants
  (about 20% lightness, text at 90%; check 4.5:1). The dark tiles use solid `#0D47A1` / `#B71C1C` / `#1B5E20` today and outshine the balance card.
- **Income / expense / net colours** per decision 1: the three converters read `IncomeColor` / `ExpenseColor` instead of literals; the Net
  tile uses the sign (income token positive, expense token negative), not a third hue.
- **Typography (verify first).** Labels look like Open Sans and amounts like Roboto. Check which controls lack the registered `FontFamily`
  (`AutoFitLabel`, `Entry`); set it in one style if so. MAUI has no OpenType feature switch, so tabular figures only if the face has them
  by default; drop this item if the digits already align.
- Tests: none in Core. Screenshots: light and dark, Home, Transactions, Reports, Add, setup wizard.

### 20b.2 Add form redesign (Core small + App, 1 PR; the biggest lever)
Confirmed: the keypad hides the pinned Save (the user types, closes the keyboard, then saves); Save is always enabled and a modal says
"invalid amount"; the amount shows bare digits (`48.50`) although `CurrencyMaskBehavior ShowCurrencySymbol="True"` is set; the order is amount,
payee, category, account, date; account and date are native pickers with the long date; bold 16 sp labels over every field.
- **Save above the keyboard.** First try `Application.On<Android>().UseWindowSoftInputModeAdjust(WindowSoftInputModeAdjust.Resize)` (no
  `windowSoftInputMode` is set on `MainActivity` today); verify with a short screen and the form scrolled. Decision 3 covers the number pad.
- **Save disabled until valid** (`CanExecute`: amount parses and is above zero); no modal. The amount placeholder says "Enter an amount".
  Other validation (account, trip, pocket cover prompt) is unchanged.
- **Currency visible.** Find why the symbol is not shown and fix it, or show the account's currency code as a small prefix next to the amount.
- **Order:** amount, category, account, date, then payee (optional) and the existing "More details". Category grid unchanged (recent first).
- **Account chips** when the user has 1 to 4 own accounts (`IAccountRepository.GetOwnAsync`), the picker for more; the selected account is
  remembered as today (`EntryDefaultsService`). **Date chips** `Today | Yesterday | Pick date` (the picker opens on Pick; the chosen date
  shows on the chip). Future dates, the schedule route and `CanEditDate` / `MinimumDate` rules stay exactly as they are; in schedule mode
  only "Pick date" shows. Core: `EntryDatePresets` (today, yesterday from a `TimeProvider`), unit tested.
- **Type switch** at the top of the form: `Expense | Income | Transfer`. Expense and Income are the same page with `Type`; Transfer
  navigates to `AddTransferPage` (replace route, carrying the amount) and back. The add button keeps its sheet (decision 2), so the type is
  always a deliberate pick and the switch only fixes a wrong one. **Make the type unmistakable:** the title and the Save button say "Add
  expense" / "Add income" / "Add transfer" (not a bare "Save"), and the header and the amount take that type's token colour (expense, income,
  transfer). The category grid already differs by type (Food and Shopping versus Salary), a second visible hint. A saved entry cannot change
  its type (confirmed by the owner), so the switch is hidden in edit mode; the title still names the type ("Edit expense").
- **Compact labels:** field captions become 12 sp secondary text (the `SecondaryLabel` style), the section titles stop being bold 16 sp.
  No floating-label library; MAUI has none built in and the handler customisation is not worth it.
- Files: `AddTransactionPage.xaml(.cs)`, `AddTransactionViewModel.cs`, `AddTransferPage.xaml`, `AddEntryButton`, `AddEntryViewModel`,
  `EntryDefaultsService`; Core: `Helpers/EntryDatePresets.cs`. Strings: `EnterAmount`, `Today`, `Yesterday`, `PickDate` (check for existing keys).
- Verify: tap count for a repeat expense (FAB, amount, category, Save = 3 taps after typing), 2.0 font, dark mode, a split, a trip, a
  future date, a schedule, a foreign-currency account, the pocket cover prompt, a transfer switched from an expense.

### 20b.3 Period stepper and Transactions rework (Core + App, 1 PR)
Confirmed: "Calendar Month" is a bare underlined `Picker` on Home, Transactions, Budgets and Reports (about 100 dp each); Transactions
spends about 25% of the screen on search, five chips and two text links before the first row; the grouping link is a three-state cycle
(Category, Account, List View) whose label shows only the current mode; it opens grouped by category with collapsed cards; the list view
has no date headers and repeats the full date on every row.
- **`PeriodStepper`** control replacing `PeriodPicker`: `‹ October 2026 ›`; a tap on the label opens a `BottomSheet` with the period types and
  the custom range; next is disabled at the current period. Core: `PeriodViewModel.Step(int)` over a new `PeriodHelper.Shift(period, n)`
  (months and years roll; Today / last-week / custom types have no step: the arrows hide). Label through `DateFormatter` (month name in the
  current culture). Tests: month and year rollover, never past today, custom ranges, labels in en and es.
- **Transactions:** search behind a toolbar icon (the `SearchBar` slides in, `Debouncer` as now); one **Filter** button with a count badge
  opens a `BottomSheet` with Type, Category, Account, Amount, Tag; active filters show as removable chips under the header (nothing when
  none); group and sort go into the overflow menu (`ToolbarItem Order=Secondary`).
- **Default grouping: by date** with the entries visible, header per day with that day's totals per currency (`CurrencyAmount`, never
  added across currencies, hidden while Hide balances is on). `LedgerGrouping` gains a Date mode; Category and Account stay as options.
  Persist the choice (`Transactions.Grouping`); the default applies only when none was saved. In date grouping rows drop the repeated date.
  MAUI's Android `CollectionView` has no sticky group headers, so none are built.
- Files: `Controls/PeriodPicker*` (replaced), the four pages, `TransactionsPage.xaml`, `TransactionsViewModel`, `TransactionFilterViewModel`
  (Core), `LedgerGrouping`, `PeriodViewModel`, `PeriodHelper`. Stage 23.6 saved filters hook into the filter sheet.

### 20b.4 Reports, Budgets, Home (App + small Core, 1 PR)
- **Reports tabs:** the fifth tab ("NET WORTH") is cut off in both themes and easy to miss. Sentence case, no letter-spacing, 14 sp so
  four tabs fit and part of the fifth shows; or group Cash flow and Net worth under Trends if that is not enough. Check what the Shell
  top tabs allow (`TextAllCaps` has no setting in the app today).
- **Donut palette:** the 7-slice donut puts green next to teal. Re-pick `ChartPalette` slots for colour-vision deficiency (adjacent slots
  must differ by shape of hue and lightness); extend the existing `ChartPaletteTests` with a minimum distance under a deuteranopia simulation.
  The "vs previous period" delta gets a meaning: spending up uses the expense token, down the income token; "Nothing in the previous period"
  stays neutral.
- **Budget card:** one status line instead of three restatements of the limit ("Per Month · $100.00", "$215.80 of $100.00", "Exceeded
  $115.80"); an 8 dp rounded bar that shows overflow (216% must not look like 100%); sort by urgency; room for Stage 22's "$X/day left".
- **Home:** replace "(Tap for details)" with a chevron; the eye toggle gets a 48 dp hit area; a slim "This month: -$1,770 spent" line
  under the balance; the primary currency line labelled when several currencies show. The Stage 22.2 safe-to-spend line goes here.

### 20b.5 More and Settings structure (App, 1 PR)
- **More:** 11 flat rows become four headed groups: Money (Accounts, Categories, Payees, Transfers), Planning (Recurring, Goals, Savings
  challenge), Shared and travel (Debts, Trips, Parties), App (Settings). `MoreViewModel.BuildItems` returns groups; keep every route.
- **Settings:** section headers (Appearance, Money, Data and backup, Privacy and security, About) over the existing cards, and a search
  field that filters the cards by title; no card is removed or moved between pages.

## Localization
Every new string in `AppResources.resx` + `.es/.pt/.fr/.it` (`ResourceParityTests`; `/translate-moneyrecord`): add-form type switch and date
chips, the filter sheet title and badge, group names in More, the Settings section headers, the budget status line, the period stepper
accessibility labels. Counts through `Plurals`, dates through `DateFormatter`. Check each language for longer labels at 2.0 font.

## Tests (`MoneyRecord.Tests`)
`EntryDatePresetsTests`, `PeriodHelper` shift / step tests, `LedgerGrouping` by date with per-currency day totals, `ChartPaletteTests`
colour-vision distance, `ResourceParityTests` (existing). Everything visual is verified on the emulator.

## Verification
`dotnet test`; Android Debug on the emulator (`/run-moneyrecord`), each PR: light and dark, font scale 1.0 and 2.0, English and Spanish,
a fresh install and a list of 9+ rows, the app lock on, Hide balances on. Screenshots before and after in each PR. Release (R8) APK smoke
test before 1.0.0.

## Risks
- A wrong entry type is the main risk of the form change: the sheet stays, the type is named in the title and Save button and coloured,
  the switch is one tap, and undo (20.1) covers a wrong save.
- Replacing the period picker touches four screens that share `PeriodViewModel` (a singleton); test that all four follow one period.
- Changing the Android soft-input mode can move other forms (bottom sheets, Settings entries); test those.
- Colour changes can break the 4.5:1 contrast already audited in 20.5; run it after 20b.1.

## Out of scope
A new design system or Material 3 library (none was verified as maintained and permissively licensed; check before adopting), Lottie
animations, onboarding overlays, a bottom-bar redesign, tablet layouts, iOS / Windows parity ("Android first").

## Confirmed on device (2026-10-30, emulator pixel_7 API 36, Debug, fresh install, English)
1. Status bar: dark clock and icons on the purple Shell bar in light and dark; the setup wizard shows a normal white bar.
2. Add expense: keypad covers the pinned Save; amount shows no currency symbol; Save always enabled; native pickers and long date; payee
   before category; fields behave in dark mode, Save visible once the keypad is closed.
3. Transactions: opens grouped by category (collapsed); grouping link cycles Category, Account, List View; list rows repeat the full date;
   five chips and two links above the list; at the end of a 9-row scroll the last row clears the add button.
4. Budgets: the add button opens "Add Category to Budget"; a $100 budget with $215.80 spent shows a full-width red bar and states the limit
   three ways; the first budget triggers the "Get reminders?" prompt.
5. Reports: five all-caps tabs, the fifth cut off in both themes; the dark donut is readable.
6. More: 11 flat rows. Settings: a stack of large cards, no section headers.
7. Home dark mode: saturated solid tiles outshine the balance card.
8. Font scale 2.0: the Net tile reads "-$1,405.5" (last digit cut); "Total Balance (Tap for de..."; tab labels "Trans...", "Repor...", "Budg...";
   add form category names break mid-word (Transport/ation, Entertain/ment) while Food and Utilities render about twice as large; payee
   placeholder clipped; the add button covers an amount on Home.
9. Not checked: tablets, TalkBack. Not confirmed: whether two fonts are really mixed.
