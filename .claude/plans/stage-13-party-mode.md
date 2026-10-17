# Stage 13: Party Mode

**Status**: Idea (to be developed with the user before any work starts)
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

## Open questions (develop later)
- Equal shares only, or weights / "not in this expense" per participant?
- Does the owner's own part become a normal expense in the ledger (and settle-up payments transfers / debts, 10.8)?
- One currency per event, or several?
- Is a participant list reusable (friends list) across events?
- Relation to Stage 12 (Travel Mode): a shared trip could be a trip + a party; decide on one "group" model or two.
- Backup format (new table(s) → format bump).

## Verify
Defined once the design is agreed: Core tests for shares, rounding and settle-up + a `/run-moneyrecord` flow (en + es).
