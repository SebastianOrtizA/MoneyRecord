# Stage 14: Pockets (money set aside inside an account)

**Status**: 14.1 done; 14.2 in review (design agreed 2026-10-17)
**Depends on**: 10.6 savings goals
**Complexity**: Medium (3 PRs)
**Source**: User request, 2026-10-17

## Goal
Right now a savings goal counts the **whole balance** of the accounts linked to it, so saving for a trip needs a
separate account even when the money really sits in the main bank account. A **pocket** is a named part of a real
account (Bank → Travel). Example: Bank 2000 USD with Travel 500 → Bank still shows 2000, and 500 of that is marked
as set aside. Balances, totals, reports, Net worth, transfers and CSV keep counting only the real account. A pocket
is only an earmark, and a goal can track it.

## Decisions (2026-10-17)
- **Allocation, not a transfer.** A pocket has its own movements ("set aside" / "take out"). The account's balance
  never changes, so balances, reports, the Transfers list, CSV and the negative-balance check don't need to know
  about pockets.
- **Goals link accounts or pockets.** One goal per account or pocket, same currency. Existing goals are unchanged.
- **A pocket is filled only from its own account.** To fund it from Cash, the user first transfers Cash → Bank.
- **Overspend: ask.** If saving an expense or a transfer out would leave the account with less than its pockets
  hold, ask which pocket to take the difference from. The user can also keep the pockets as they are (the account
  then shows a negative "Available"), or cancel.

## Sub-stages (branch + PR each)

### 14.1 Core
- Models (`MoneyRecord.Core/Models/`), created from the models with no migration (like the party tables):
  - `Pocket`: Id, `AccountId` (`IX_Pocket_AccountId`), Name, IconCode, CreatedDate. Its currency is always its account's.
  - `PocketMovement`: Id, `PocketId` (`IX_PocketMovement_PocketId`), signed `AmountCents` (+ set aside, − take out),
    Date, optional `TransactionId` / `TransferId` (the entry it covered), optional Note.
  - `SavingsGoalPocket`: GoalId, `PocketId` (unique index). This mirrors `SavingsGoalAccount`.
  - Display model `PocketSummary`: pocket, account, balance, currency, texts.
- No new repository. `IPocketService` / `PocketService` uses `DatabaseInitializer` directly, like
  `SavingsGoalService`:
  - `GetBalancesAsync()`: SQL `SUM(AmountCents) GROUP BY PocketId`. `GetTotalsByAccountAsync()` gives what each
    account has in pockets.
  - `GetPocketsAsync(int? accountId)` → `PocketSummary` list.
  - `SaveAsync(Pocket)`, `DeleteAsync(pocketId)`: removes the movements and the goal link in one transaction. The
    money simply goes back to "available".
  - `MoveAsync(pocketId, amount, date, note)`: setting aside is refused above what is available (account balance
    − pockets). Taking out is refused above the pocket's balance. Refusals throw a `PocketProblemException` with a
    reason enum, like `PartyProblemException`.
  - `GetShortfallAsync(accountId, decimal extraOutflow, DateTime date)`: how much the pockets would exceed the
    balance after a new outflow. Returns 0 for a future-dated entry, following `IBalanceService.CountedBefore`.
  - `CoverAsync(IReadOnlyList<(pocketId, amount)>, transactionId?/transferId?)`: saves the take-out movements.
  - Pure helper `PocketCover.Plan(shortfall, pocket balances, chosen pocket)` in `Helpers/`: takes
    min(shortfall, pocket balance) and returns what is still short. Tested.
- `SavingsGoalService`: `GetProgressAsync` adds linked pockets. Saved = account balances + pocket balances. The
  pace counts pocket movements since the pace start, added to `GetChangesSinceAsync` for accounts.
  `SaveAsync(goal, accountIds, pocketIds)` refuses a pocket whose account is in the same goal (that would count the
  money twice), a pocket in another currency, or a pocket that belongs to another goal. `GoalProgress` takes pocket
  names too ("Savings · Bank › Travel"). A pocket whose account changed currency is listed as uncounted, like accounts.
- `AccountService.DeleteAsync`: also deletes the account's pockets, their movements and goal links, in the same
  transaction.
- `DataExportService`: backup format **v6** adds `Pocket`, `PocketMovement`, `SavingsGoalPocket` (to the restore
  and the fingerprint). Restoring v5 or older gives no pockets. CSV is unchanged.
- Tests (`MoneyRecord.Tests/Services/PocketServiceTests.cs`, plus additions to `SavingsGoalServiceTests`,
  `AccountService` delete and backup round-trip tests): balances, available, refusals, shortfall (including the
  future-dated case), cover plan, goal with pockets plus the double-count refusal, delete cascade, v6 round-trip and
  v5 restore.

### 14.2 Accounts UI + Goals
- `AccountListItem`: when an account has pockets, it shows `InPockets` and `Available` ("Available $1,500 · $500 in
  pockets"). Available goes red when it is negative. Pocket rows sit indented under the account card on
  `ManageAccountsPage` (icon, name, amount).
- The account sheet gets a "New pocket" button. Tapping a pocket row opens a second `BottomSheet` on the Accounts
  page with name + icon, **Set aside** / **Take out** (amount with `CurrencyMaskBehavior` in the account's
  currency), the last movements, and Delete (confirm, saying the money goes back to the account).
  `ManageAccountsViewModel.CloseTopOverlay` closes this sheet first.
- Account sheet: changing the currency of an account that has pockets gets a warning, like the goal one.
- `GoalsViewModel.BuildAccountChoices`: the choices include pockets in the goal's currency ("Bank › Travel"). An
  account and its own pockets can't both be ticked: ticking one unticks the other. `GoalAccountChoice` gets a kind
  (account or pocket).
- Add-form hint: the transfer form's `SourceAccountBalanceText` (and the expense form's account line, where present)
  adds "· $500 in pockets".
- Strings in en + es ("Pockets" / "Bolsillos", "Set aside" / "Apartar", "Take out" / "Sacar", ...).
  `ResourceParityTests` must pass.

### 14.3 Cover-the-shortfall prompt
- `AddTransactionViewModel` (expense save, new or edited, single or split) and `AddTransferViewModel` (source
  account): **before** saving, compute the extra outflow. For an edit on the same account that is the new amount
  minus the original amount (the `_originalAmount` pattern in `AddTransferViewModel`). Then call
  `GetShortfallAsync`. If it is above 0, show an action sheet through `IDialogService` (`PickList` for unique
  labels): "This uses $100 you set aside. Take it from:" with "Travel ($500)", "Emergency ($200)", and "Keep
  pockets as they are". Cancel = don't save. If the chosen pocket can't cover it all, ask again with the remaining
  pockets (`PocketCover.Plan`). After the entry saves, `CoverAsync` records the take-outs, linked to the entry.
- The loop lives in Core as `PocketCoverPrompt` (it uses `IDialogService` + `IPocketService`) so it can be tested
  with NSubstitute and both forms share it.
- Paths with no UI (recurring processing in `ReminderService`, CSV import, debt payments, party "my share") never
  prompt. They leave the account's Available negative, and the Accounts card shows that.

## Critical files
- New: `MoneyRecord.Core/Models/Pocket.cs`, `PocketSummary.cs`; `Services/PocketService.cs` +
  `Interfaces/IPocketService.cs`; `Helpers/PocketCover.cs`; `ViewModels/PocketCoverPrompt.cs`
- Changed: `Services/SavingsGoalService.cs`, `Models/GoalProgress.cs`, `Models/SavingsGoal.cs`,
  `Services/AccountService.cs`, `Services/DataExportService.cs` (BackupVersion 6), `Models/AccountGroup.cs`,
  `MoneyRecord/ViewModels/ManageAccountsViewModel.cs` + `Views/ManageAccountsPage.xaml`, `GoalsViewModel.cs`,
  `AddTransactionViewModel.cs`, `AddTransferViewModel.cs`, `MauiProgram.cs` (singleton `IPocketService`),
  `AppResources*.resx`, `CLAUDE.md` (models list, services, backup v6), README Features
- Reused: `DatabaseInitializer.RunInTransactionAsync`, `IBalanceService.GetAllAccountBalancesAsync` /
  `CountedBefore` / `GetChangesSinceAsync`, `BottomSheet`, `CurrencyMaskBehavior`, `AmountParser`, `PickList`,
  `MoneyFormatter.SameCurrency`, `CategoryIconService.GetAccountIcons`

## Verify
- `dotnet test MoneyRecord.Tests/MoneyRecord.Tests.csproj`: new pocket, goal, delete and backup tests, plus
  resource parity.
- Android build + `/run-moneyrecord`, in English and Spanish:
  1. Bank 2000 → new pocket Travel, set aside 500. The card shows 2000, "Available 1,500 · 500 in pockets". The
     Home balance and Net worth are unchanged.
  2. Goal "Trip" linked to Bank › Travel shows 500 of its target. Ticking Bank unticks Travel.
  3. A 1,800 expense from Bank triggers the prompt. Pick Travel: the pocket drops to 200 and Available is 0. Repeat
     with "Keep pockets": Available goes negative (red). Cancel saves nothing.
  4. Take out the rest, then delete the pocket. Backup → restore keeps the pockets and goal links. An older (v5)
     backup restores with no pockets.
