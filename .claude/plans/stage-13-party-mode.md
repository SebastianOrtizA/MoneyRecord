# Stage 13: Party Mode

**Status**: Design agreed 2026-10-17 (decisions below); 13.1 done, 13.2 in review
**Depends on**: Stages 6–11 (modernization); related to 10.7 splits and 10.8 debts
**Complexity**: TBD
**Source**: User request, 2026-10-14

## Goal
Organise the money of a shared event (party, dinner, group trip): a budget, the expenses, what each participant put
in, the total, and who owes whom so that everybody ends up paying the same share.

## Idea (first notes)
- An event = name, date, budget, currency, participants (names only; no accounts or sign-in).
- Expenses: amount, description, who paid. Contributions: money a participant hands in up front (a common pot).
- Summary: total spent vs budget, share per person (total / participants), each person's balance
  (paid + contributed − share).
- **Settle up**: list the fewest payments that bring every balance to zero (match the largest debtor with the
  largest creditor, repeat), shown as "Ana pays Luis $20". Mark payments as done.
- Remainder cents after an even split go to named people deterministically, so totals always match exactly.
- Share the summary / settlement as text or an image (no network needed).

## Decisions (2026-10-17)
- **Shares**: equal split per expense, with exclusions (untick who wasn't part of it). No weights.
- **Ledger link**: standalone. Settle-up is a list whose payments are marked done by hand; no accounts, transfers or
  debts (10.8) involved. Optionally record the owner's own share as a normal expense (later, 13.3).
- **Currency**: one per event. **Participants**: a reusable friends list (a `Participant` table; an event links
  participants by ID), so names are typed once.
- Still open: relation to Stage 12 trips (leaning to keep them separate), backup format bump (v5 for the new tables).

## Proposed sub-stages
- 13.1 Core (done): `Participant`, `PartyEvent`, `PartyMember`, `PartyExpense` (+ `PartyExpenseExclusion`), `PartyContribution`; the
  tables come from the models (no migration needed); `PartyLedger` (even split with rotating leftover cents, balances,
  settle-up; the common pot is line ID 0 so balances sum to zero), `IPartyService` / `PartyService`; tests.
- 13.2 UI (in review): More → Parties (cards + new/edit sheet with friends ticked), Friends page, party page (spent of budget,
  balances with the pot line, expenses and contributions with sheets; tap = edit / delete); `PartyProblemException` names
  why a change was refused; display models `PartyCard` / `PartyBalanceRow` in Core (tested).
- 13.3 Settle-up screen (mark paid), share as text, optional own-share expense, backup/restore v5.

## Verify
Defined once the design is agreed: Core tests for shares, rounding and settle-up + a `/run-moneyrecord` flow (en + es).
