# Stage 15: Variable-amount recurring (bills that change each time)

**Status**: 15.1 in review (core, no UI yet); 15.2 not started. Design agreed 2026-10-25 (decisions below)
**Depends on**: 11.7 scheduled recurring
**Complexity**: Medium (2 PRs)
**Source**: User request, 2026-10-25

## Goal
Some entries are regular but their amount changes: salary, electricity, water, phone usage. A recurring schedule marked
**amount varies** never creates the entry by itself. Each due occurrence waits in a **To enter** list, the user is
notified, and a tap opens the add form already filled in. The user types the real amount, or skips that occurrence.

## Decisions (2026-10-25)
- **Pending "to enter", nothing is saved automatically.** (The owner rejected auto-creating an estimate and a
  reminder-only variant.)
- No new table. `RecurringTransaction.IsVariableAmount` (bool) and `LastAmountCents` (the last amount entered, used as the
  estimate). `CreateTableAsync` adds both columns, so no migration is needed. Backups keep working: an old backup restores
  with the flag off (the backup format does not change for this).
- The pending occurrences are the dates from `NextOccurrence` through today (`UpcomingSchedule.Occurrences`). Entering or
  skipping the **oldest** one advances the schedule (the same `Advance` as processing), so nothing extra is stored and a
  missed month shows up as two pending rows.
- `RecurringTransactionService.ProcessDueAsync` skips variable schedules (in `RecurringTransactionRepository.GetDueAsync` or
  in the loop). A debt's schedule is never variable.
- Estimates are display only: Upcoming shows "≈ $85" (from `LastAmountCents`), or "Amount varies" without one. Nothing sums
  the estimates into totals.

## Sub-stages (branch + PR each)

### 15.1 Core
- Model fields above. `UpcomingEntry.IsVariable` and `IsPending` (date ≤ today); `UpcomingSchedule.Build` fills them in.
- `IRecurringTransactionService`:
  - `GetPendingAsync()`: variable occurrences due through today, oldest first.
  - `EnterOccurrenceAsync(recurringId, Transaction)`: in one `RunInTransactionAsync`, inserts the transaction, sets
    `LastAmountCents` and advances the schedule. It refuses a date that is not the schedule's `NextOccurrence`.
  - `SkipOccurrenceAsync(recurringId)`: advances the schedule (and deactivates it after `EndDate`, like processing).
- `ReminderService.RunDailyAsync`: notifies each new pending occurrence once (new `PendingIdBase = 500_000`, preference key
  `Reminders.Pending.`); the route opens the add form with `RecurringId` + `Occurrence`. Bill reminders for a variable one say
  "amount varies" instead of an amount.
- Tests: `RecurringTransactionServiceTests` (not auto-created, enter advances and stores the last amount, skip, catch-up of
  two occurrences, end date), `UpcomingScheduleTests`, `ReminderServiceTests` (pending notified once).

**Done as (15.1)**: as planned. Notes: `GetPendingAsync` = `GetUpcomingAsync(0)` filtered to `IsPending`; Enter/Skip share one
transaction and refuse a non-variable or inactive schedule, an occurrence not yet due, and (Enter) a date other than
`NextOccurrence`; `UpcomingEntry.EstimateText` ("≈ $85" / "Amount varies") feeds the notification and 15.2's rows; pending
occurrences are left out of the bill reminders (the pending notification covers them). The notification route
(`ReminderService.PendingRoute` = `AddTransactionPage?RecurringId=..&Occurrence=yyyy-MM-dd`) is read by the add form in 15.2,
so until then a tap opens the plain add form.

### 15.2 UI
- Add form in schedule mode (`AddTransactionViewModel`, `Schedule=true`): an "Amount varies" switch; with it on, the amount is
  optional for the schedule. Recurring edit form (`RecurringTransactionsViewModel`): the same switch.
- Add form in "enter occurrence" mode (query `RecurringId` + `Occurrence`): prefilled with category, account, payee,
  description and date, with an empty amount (hint: the last amount). Save calls `EnterOccurrenceAsync`. This reuses the
  existing form and `NavigationService`.
- Recurring page: a **To enter** section at the top of Upcoming with Enter and Skip (confirm). Home's Upcoming card shows
  pending rows first with an "Enter" badge. `UpcomingRow` shows "≈" or "Amount varies".
- Strings in en + es; `ResourceParityTests`.

## Critical files
- Changed: `Models/RecurringTransaction.cs`, `Models/UpcomingEntry.cs`, `Helpers/UpcomingSchedule.cs`,
  `Services/RecurringTransactionService.cs` + interface, `Services/Repositories/RecurringTransactionRepository.cs`,
  `Services/ReminderService.cs`, `MoneyRecord/ViewModels/AddTransactionViewModel.cs`,
  `RecurringTransactionsViewModel.cs`, `MainViewModel.cs`, `Views/RecurringTransactionsPage.xaml`, `Controls/UpcomingRow`,
  `AppResources*.resx`, `CLAUDE.md`
- Reused: `RecurrenceCalculator`, `UpcomingSchedule.Occurrences`, `DatabaseInitializer.RunInTransactionAsync`,
  `NotificationLaunch`, `INavigationService`

## Verify
- `dotnet test MoneyRecord.Tests/MoneyRecord.Tests.csproj`.
- Android build + `/run-moneyrecord`, English and Spanish: a monthly variable "Electricity" starting today creates nothing in
  Transactions but shows a pending row and a notification; tapping it opens the form prefilled; entering 85 saves it and the next
  month shows "≈ 85". Skip works. Fixed schedules behave as before.
