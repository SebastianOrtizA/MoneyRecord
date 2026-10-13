# Stage 8: UX Overhaul

**Status**: Complete
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
  `MoneyFormatter.FormatSigned` (+$85.00) via `SignedMoneyConverter`. Recent has "See all" →
  `//TransactionsPage`, Budget summary "See all" → `//BudgetPage` (`CardLink` style), and its rows show "spent /
  limit". Dark-mode amounts on the Income / Expense / Net cards are near-white (red on red was unreadable). N7: the
  filter toggle used the filter-remove icon, so it looked like a clear button; it is a plain filter icon now and the
  Clear button only shows with an active filter.

### 8.3 Transactions tab
- Full list moved off Home: search, filter chips (type, category, account, amount, tags), group by none/category/account.
- Swipe to edit/delete; tap opens a detail sheet (with receipt preview).
- Proper localized empty state.
- Done as: one `CollectionView` (header = period, search, chips, count / Clear, grouping and sort links; footer =
  empty state) instead of two lists inside a `ScrollView`; `LedgerItemTemplateSelector` picks the row or the group
  template. Chips Type · Category · Account · Amount · Tag show their name until they filter, then the value (active
  colours, `Controls/FilterChip.IsActive` + one style trigger); tapping one opens a pick list via
  `IDialogService.ActionSheetAsync` (`PickList.UniqueLabels`: repeated names get " (2)" so the answer maps back to
  one ID), Amount opens the min/max row (`DecimalEntry`). New tag filter
  (`TransactionFilter.Tag`, `LedgerFilter.TagOptions`, `LedgerEntry.TagList`). Rows are compact (icon, description,
  category · account, amount, tag/receipt marks, date); swipe left reveals Edit / Delete. A tap opens a detail sheet
  (amount, date, account, tags, receipt preview → full-screen photo, Edit / Delete). Android back closes the photo,
  then the sheet, then the add sheet: pages implement `IOverlayHost` instead of AppShell looking up `AddEntry` by
  name. Empty state: "No transactions in this period · Tap + to add…" or, with filters, "No transactions match these
  filters" + Clear. Removed the collapsible filter panel, the `GroupingModeToIs(Not)Grouped` converters and the
  `NoTransactions` / `StartTracking` / `AmountRange` strings.

### 8.4 Fast entry (P7)
- Field order: Amount (autofocus, numeric keypad) → Category (icon grid, recent first) → Account → Date → optional
  Description/Tags/Receipt/Recurring under "More details".
- Description optional; defaults to the category name.
- Remember last used account and category per type (`IPreferencesService`).
- Save button pinned at the bottom, always visible.
- Done as: `AddTransactionPage` is a `Grid` (scrolling form + pinned Save bar; back cancels, so the Cancel button is
  gone). Amount first, large, focused with the numeric keypad on a new entry (not when editing). Category is an icon
  grid (`FlexLayout`, 4 per row, `CategoryChoice.IsSelected` highlight). Account and Date follow; Description (its
  placeholder is the category name; when editing, one equal to the category name shows empty via
  `EntryDescription.ForEditing`, so it follows a category change), Tags, Receipt and Recurring sit under "More
  details", which opens by itself when editing an entry that has a custom description, tags or a receipt. Core `IEntryDefaultsService` (over the
  so-far-unused `IPreferencesService`) keeps per type the last account and the 4 most recent categories by ID:
  recent ones lead the grid and the last one is preselected (deleted ones fall back to the default account / first
  category). `EntryDescription.Resolve` saves an empty description as the category name. The "*" left the Date,
  Description, Amount, Category and Account labels (they were also shown on the Recurring page) and the "Required
  fields" note left both add pages; `RequiredFields` and `PleaseEnterDescription` are gone. Verified: amount → tap
  category → Save = 2 taps after the amount.

### 8.5 Accounts screen
- Cards with icon, name, currency badge, balance in its own currency, last activity.
- Grouped by currency (base first), each group with a subtotal header in that currency ("EUR · €300.00 · 2 accounts").
  Accepts a currency filter from Home.
- Add/Edit in a bottom sheet instead of the always-open form on top of the list.
- Done as: one grouped `CollectionView` of cards (icon, name, currency badge, balance in its own currency, "Last
  activity: Oct 13, 2026" from `AccountBalanceInfo.LastActivityDate`). Core `AccountGroup.Build` groups by resolved
  currency, base first then by code, with "EUR · €300.00 · 2 accounts" headers (`Plurals.Accounts`); the Home
  currency filter keeps one group, with the "EUR accounts · Show all" chip above the list. A + button opens the add
  sheet; tapping a card opens the same sheet in edit mode with Delete (hidden for the default account; the sheet stays
  open when the delete is cancelled). One set of form fields replaces the separate add/edit fields; the form scrolls
  inside the sheet with title and buttons fixed; Android back closes the sheet (`IOverlayHost`). The per-card Edit /
  Delete buttons, the initial balance on the card and the `YourAccounts` / `InitialBalanceAmount` strings are gone.
  Known, not fixed here: the icon-tile converters read the theme when they run, so a live light/dark switch leaves
  stale tile colours until the page is reopened (also on Categories).

### 8.6 Budgets
- Edit in a bottom sheet (period, limit) instead of `DisplayPromptAsync`.
- Status colours: green / amber / red with text labels ("On track", "Near limit", "Over") so colour isn't the only signal.
- Empty state with a budget/target icon instead of "#" (N8).
- Done as: one `CollectionView` (header = period picker and totals, footer = empty state) of cards: category icon,
  name, "Per Month · $100.00", a status label with its own icon and colour (`BudgetProgress.StatusText` /
  `StatusIcon` from `Level`: On track < 60% ≤ Near limit ≤ the limit < Over budget; review: `Exceeded` now means
  strictly over, like `IsOverBudget`, so exactly the limit is "Near limit" + "Remaining $0.00"), the bar, "$70.00 of $100.00" and
  Remaining / Exceeded. A + button opens the add sheet (category, limit, period; a toast when every expense category
  already has a budget); tapping a card opens the edit sheet, which now edits the period too
  (`IBudgetRepository.UpdateLimitAsync` replaces `UpdateAmountAsync`) and holds Delete. N8: `F117F` rendered as a boxed
  "#"; the empty state uses bullseye-arrow (`F08C9`). The page title is "Budgets" (was "Budget Mode").
  **Also:** the 8.5 review asked for a shared sheet once a fourth appeared, so `Controls/BottomSheet` (dim + rounded
  panel + handle + slide-up, one padding, `IsOpen` / `CloseCommand` / `Opened`) now hosts all four sheets (add entry, transaction
  details, account form, budget form) and `SheetAnimation` is gone. Removed the four `BoolToOverBudget*` colour/icon
  converters and the `BudgetMode`, `YourBudgets`, `EnterNewBudgetLimit`, `OriginalLimit`, `ProjectedLimit`, `Spent`
  strings.

### 8.7 Reports hub (P9) — LiveChartsCore
- Add `LiveChartsCore.SkiaSharpView.Maui` (MIT, 2.0.5); retire `Controls/ChartDrawables.cs`.
- Tabs: **Spending** (donut + ranked list) · **Income** · **Trends** (monthly bars) · **Cash flow** (income vs
  expense line) · **Net worth** (sum of account balances over time).
- Period picker shared across tabs; "vs previous period" delta on every headline number.
- Tap a slice/bar → Transactions tab pre-filtered to that category/month.
- Budget vs actual per month for budgeted categories.
- Split in two PRs: **8.7a** = the library, the charts and the tab shell (bullet 1, Spending / Income / Trends of
  bullet 2); **8.7b** = Cash flow and Net worth tabs, the shared period picker with "vs previous period" deltas,
  drill-down to Transactions, budget vs actual.
- 8.7a done as: `LiveChartsCore.SkiaSharpView.Maui` 2.0.5 (MIT; brings SkiaSharp 3.119, MIT) registered with
  `UseSkiaSharp().UseLiveCharts()`; `Controls/ChartDrawables.cs` and the heatmap (`HeatmapColorConverter`,
  `CategoryAmount.HeatLevel`, 16 heatmap / analysis strings) are gone. Reports top tabs are Spending · Income · Trends
  (routes unchanged). Spending / Income: a donut with the total in its middle and the ranked list as its legend and
  table view (dot = slice colour, amount, share, share bar; a tap still opens the transactions popup). Colours: the
  dataviz reference categorical palette, light and dark steps chosen separately (`Charts/ChartPalette`), the 8 largest
  categories of the report get one each, chosen by the category's place among its type's categories by ID
  (`CategoryAmount.PaletteSlot`, wrapping; a larger category already holding it pushes to the next free slot), so
  colour follows the category, not its rank; the smaller rest fold into one grey "Other" slice (`CategoryReport.Slices`;
  review: "Other" by ID made a large newer category grey). Income / expense / card colours come from `Colors.xaml`
  (`CardBackground` + shared `ChartCard` style), so the chart gaps match the card. Trends: monthly spending and
  income vs expenses as rounded columns (one legend only on the two-series chart), money on the value axis, a short
  localized month under every bar (`DateStyle.Month`), totals from Core `MonthlyTotalsBuilder` instead of loops in
  the view model; the expense-category pie left Trends (the
  Spending donut shows it). `Charts/ChartFactory` holds the shared look (2px surface gaps, 4px rounded bar ends,
  recessive grid). The palette validator needs Node, which this machine lacks; the palette is used unchanged from the
  reference, validated there against near-identical surfaces.
- 8.7b done as:
  - **One shared period**: Core `PeriodViewModel` (singleton) holds the period and custom dates; Home, Transactions,
    Budgets and Spending / Income all bind `Controls/PeriodPicker` to it (the four copies of the picker markup and
    their `OnSelectedPeriodChanged` handlers are gone, as the 8.3 TODO asked). A page follows `Changed` only while it
    is shown (`WatchChanges` in `InitializeAsync`, disposed in `Deactivate` on disappearing; review: every hidden tab
    reloaded on each change) and reloads anyway when it appears. Home now also offers "Today" (one period list for
    all). The time-series tabs (Trends, Cash flow, Net worth) need whole months, so they share `MonthCount`
    (3 / 6 / 12, `Controls/MonthCountPicker`) instead.
  - **"vs previous period"**: `DateRangeHelper.GetPreviousRange` (whole calendar months move back by months, any
    other range by its length) and `PeriodDelta` ("▲ 12% vs previous period", "Same as…", "Nothing in…", ">999%"
    past ten times) under every headline: Spending / Income totals, Trends spending, Cash flow net, Net worth.
  - **Tabs**: Spending · Income · Trends · Cash flow · Net worth (top tabs scroll). Trends = spending per month +
    **budget vs actual** per month (Core `BudgetVsActualBuilder`: exact calendar-month limits, a budget counts from
    the month it was created). Cash flow = income and expense lines, headline net (`MonthTotal.Net`). Net worth =
    month-end totals of the base-currency accounts worked back from today's balances (Core `NetWorthBuilder`), with
    "Accounts in COP only" when other currencies exist (never added together). The months share
    `MonthlyReportViewModel`.
  - **Drill-down**: a slice or list row (Spending / Income) opens Transactions for the same period filtered to that
    type and category ("Other" = the type only); a bar or point opens that month (it becomes the shared period) and
    type. Core `TransactionsDrillDown` hands the request over (taken once on the next load) and
    `TransactionFilterViewModel.ShowOnly` applies it; the category transactions popup and its strings are gone.
    `Charts/ChartTapBehavior` hit-tests on a real tap and runs the handler kept on the series' `Tag` (the chart's
    own pointer-down also fired when a scroll started on a chart and navigated away).
  - **Also**: `BudgetProjectionHelper` counts a custom range of whole calendar months in months (a drill-down sets
    one, and October showed a $50 monthly budget as $51.67), with the same rule as the previous range
    (`DateRangeHelper.WholeMonths`). Removed `PeriodHelper.GetPeriods` /
    `GetDefaultPeriod` and 8 unused strings.
  - The four list pages (Spending / Income, Budgets, Accounts, Transactions) keep their 16px gutter inside the
    header, rows and footer instead of the CollectionView's margin, so the scrollbar sits at the screen edge like the
    ScrollView pages (user feedback on 8.7b).
  - Known, unchanged: totals across accounts still add currencies in the base currency (the Spending total adds USD
    expenses to a COP base) until Stage 10.3.

### 8.8 First-run setup
- 3 steps: language & base currency → starting accounts with balances (Cash pre-filled) → pick default categories.
- Skippable; reachable later from Settings.
- Done as `Views/SetupPage` over a Core `SetupViewModel` + `ISetupService`. A new install (no database file at the
  first start; decided once and saved as `SetupState`, so closing the app halfway shows it again) opens the window
  on the setup instead of the Shell; finishing swaps in `AppShell`. Updates from older versions skip it. Step 1 sets
  the language (System / English / Español, saved as the `Language` preference and applied at startup; this is the
  language half of 8.9's picker) and the base currency, both live. Step 2 lists starting accounts in the base
  currency (Cash pre-filled, add/remove rows). Step 3 lists `DefaultCategory.All` (now also the seed list) with
  checkboxes. First run: one transaction replaces the seeded Cash and categories with the choices (only while
  nothing is recorded), so they're named in the chosen language. Skip = finish with what the steps show. From
  Settings ("Setup assistant" card, pushed, tab bar hidden) it only adds: no prefilled account, existing default
  categories are checked and locked. Android back goes to the previous step first (`IOverlayHost`; MainActivity now
  sends back to the window's page, so it also works before the Shell exists). `DialogService` shows dialogs on the
  window's page for the same reason.
  **Fixes found on the way:** `{ext:Localize}` bindings never refreshed on a language change (`LocalizedStrings`
  now listens to `LanguageChanged`), and `CurrencyMaskBehavior` kept the culture it was created with, so after a
  switch to Spanish "120,00 €" was read back as 12000.

### 8.9 Settings
- Language picker (System / English / Español) using `LocalizationService.SetCulture`.
- Base currency, theme (System/Light/Dark), backup section, about/licences (list third-party licences, required for
  AGPL distribution with MIT/Apache deps).
- Done as a "Language and appearance" card at the top of Settings: language (System / English / Español, from
  `LanguageOption.All()`, shared with the setup; the saved `Language` preference came with 8.8) and theme (System /
  Light / Dark, `IThemeService` → `Application.UserAppTheme`, saved as `Theme`, applied in the App constructor before
  the first page). Both apply live. Base currency, setup assistant and backup cards stay. An "About" card opens
  `AboutPage`: version, the AGPL notice, a source-code link, and `ThirdPartyComponent.All` (name, licence, copyright).
  A tap pushes `LicensePage` with the full text from `Resources/Raw/Licenses/` (the official files: Apache-2.0, each
  MIT LICENSE, the Pictogrammers licence, SkiaSharp's LICENSE + its 140 KB third-party notices for Skia, HarfBuzz,
  FreeType, ICU…). `ThirdPartyComponentTests` fails when a package reference isn't listed or a licence file is
  missing or unused.
  **Found on the way:** after a live language change, Home's date and every period picker kept the old language
  (both built once in singletons). `MainViewModel` now re-formats the date and calls `PeriodViewModel.RefreshNames()`
  (new items, same period, no reload) on `LanguageChanged`. The README said the icon font is OFL; the current
  Pictogrammers licence says Apache-2.0 for fonts and icons, and the README now lists LiveCharts2, SkiaSharp and
  Open Sans too.

## Tests
- ViewModel tests for entry defaults (remembered account/category, description fallback).
- Report aggregation tests in Core (period deltas, net worth series).

## Verify on device (`/run-moneyrecord`)
Screenshot each tab in light, dark and Spanish; check that nothing is covered by the FAB, every icon renders, and
an expense can be added in ≤ 4 taps after amount entry.

## Done when
Bottom tabs ship, the flyout and duplicate add buttons are gone, entry is description-optional with remembered
defaults, and the Reports hub runs on LiveChartsCore with drill-down.
