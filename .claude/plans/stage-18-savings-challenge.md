# Stage 18: Savings challenge (doubling / linear game)

**Status**: Design agreed 2026-10-25 (decisions below); not started
**Depends on**: none (optional link to accounts and the 10.6 goals)
**Complexity**: Medium to large (3 PRs)
**Source**: User request, 2026-10-25

## Goal
A game: pick a goal amount, a number of days and a start date. The app works out each day's amount. The user saves any day's
amount in any order and marks it done. A calendar grid shows the done and remaining days, the total, and what is saved so far.

## Decisions (2026-10-25)
- **Two modes** (the owner chose "doubling + linear" because strict doubling only works for short challenges: $1,000 in 30 days
  would start at $0.000001 and end at $500).
  - **Doubling**: day amounts a, 2a, 4a, …. With n days, a = G / (2ⁿ − 1). When n is more than the useful number of doublings
    k = ⌊log₂(G/unit + 1)⌋ (unit = one currency unit or the rounding step), the form warns. The plan then uses k levels that
    each last several days (days spread evenly, the earlier levels take the remainder), with a = G / Σ(daysᵢ · 2ⁱ). With
    n ≤ k it is pure doubling. The form suggests k days.
  - **Linear**: day i saves i · s, with s = G / (n(n+1)/2). Fits any length.
- **Rounding** (optional): none, or to 1 / 10 / 100 / 1000 units, nearest. The form shows the **total** next to the goal ("Total
  $1,023 · $23 over the goal"). Zero-decimal currencies use `CurrencyService.GetMinorUnits`.
- **Optional money**: a challenge can link a *from* account and a *savings* account in its currency. Marking a day done then
  asks "Record a transfer of $X?" (Yes / Just mark). Unmarking a day that has a transfer asks whether to delete it too. Linking
  the savings account to a 10.6 goal makes the goal count it, with no new link.
- Each done day stores its own `AmountCents`, so the history stays right even if the plan changes. The plan's settings (goal,
  days, mode, rounding) are locked once any day is done; name, accounts and start date stay editable.

## Sub-stages (branch + PR each)

### 18.1 Core
- Models (created from the models, no migration): `SavingsChallenge` (Id, Name, GoalCents, CurrencyCode, StartDate, Days, Mode,
  RoundingCents, FromAccountId?, ToAccountId?, CreatedDate) and `SavingsChallengeDay` (ChallengeId + DayNumber unique index,
  AmountCents, DoneDate, TransferId?).
- `Helpers/ChallengePlan.Build(goalCents, days, mode, roundingCents)` returns day amounts, total and `MaxUsefulDoublings`.
  `ChallengePlanTests`: pure doubling, levels, linear, rounding totals, one day, JPY, a huge n.
- `ISavingsChallengeService` / `SavingsChallengeService` (on `DatabaseInitializer`, like `SavingsGoalService`): CRUD,
  `GetSummaryAsync` (days with amounts and done flags, saved / total / days left), `MarkDoneAsync(day, recordTransfer)` and
  `UnmarkAsync(day, deleteTransfer)`. The transfer is written in the same DB transaction, and refused in a different currency or
  when an account is gone. Deleting a challenge keeps its transfers.
- Backup: the next format version (v7 after Stage 14's v6) adds both tables; older backups restore with no challenges. The
  fingerprint is updated. Round-trip test.

### 18.2 Challenges list and create form
- More → **Savings challenge** (`ChallengesPage`): cards with name, a progress bar and "saved X of Y · n days left". A
  `BottomSheet` form has name, goal (`CurrencyMaskBehavior`), currency, days, start date, mode, rounding and the optional
  accounts. A live preview shows the first and last amount, the total against the goal, and the doubling warning with the
  suggested days.

### 18.3 Challenge page (the game)
- `ChallengePage`: a summary card, then a 7-column `CollectionView` grid (`GridItemsLayout`), one cell per day with the date
  (`DateFormatter` MonthDay) and the amount. Done cells are filled with a check, future and past cells look different, today is
  outlined. A tap toggles a cell (with the transfer prompt when accounts are linked). A "Remaining only" filter chip, and a
  menu: edit, delete.
- Strings in en + es. If Stage 16 has landed, also add a `HomeCard.Challenge` (hidden by default).

## Critical files
- New: `Models/SavingsChallenge.cs`, `Helpers/ChallengePlan.cs`, `Services/SavingsChallengeService.cs` + interface,
  `MoneyRecord/ViewModels/ChallengesViewModel.cs`, `ChallengeViewModel.cs`, `Views/ChallengesPage.xaml`, `ChallengePage.xaml`
- Changed: `DatabaseInitializer.cs`, `DataExportService.cs` (backup version), `MorePage`, `AppShell`, `MauiProgram.cs`,
  `AppResources*.resx`, `CLAUDE.md`
- Reused: `DatabaseInitializer.RunInTransactionAsync`, `CurrencyService.GetMinorUnits`, `CurrencyMaskBehavior`, `BottomSheet`,
  `DateFormatter`, the transfers table

## Verify
- `dotnet test` (plan maths, service, backup round-trip, old backup restore).
- Android build + `/run-moneyrecord`, English and Spanish: $1,000 in 10 days doubling gives day 1 ≈ 0.98 and a total of 1,000;
  rounding to 1 shows the total difference; 30 days doubling shows the warning and levels; linear 30 days. Mark days 5, 2, 9 (out
  of order) and the saved total updates. With accounts linked, the transfer appears in Transfers and goes on unmark. Backup →
  restore keeps the challenge.
