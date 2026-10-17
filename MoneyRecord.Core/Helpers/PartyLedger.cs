using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// The money of a party. Each expense is shared evenly between the members not left out of it; what each person paid or
    /// handed in less their share is their balance. The common pot is a line of its own (<see cref="PotId"/>): it is
    /// owed what its expenses cost and owes what was handed in, so all balances always add up to zero.
    /// </summary>
    public static class PartyLedger
    {
        /// <summary>The pot's ID in balances and payments; real participants have positive IDs.</summary>
        public const int PotId = 0;

        /// <summary>
        /// One expense's amount shared evenly, in cents adding up exactly. The leftover cents go to the first people in ID
        /// order starting after the expense's own ID, so they rotate between expenses but never change for one.
        /// </summary>
        /// <returns>Participant ID → share.</returns>
        public static Dictionary<int, long> ShareExpense(PartyExpense expense, IEnumerable<int> includedParticipantIds)
        {
            var ids = includedParticipantIds.Distinct().Order().ToList();
            if (ids.Count == 0)
                throw new ArgumentException("An expense needs at least one person to share it.", nameof(includedParticipantIds));

            // SplitAllocation.Share gives the extra cents to the earliest parts; rotating the order spreads them over expenses
            var shares = SplitAllocation.Share(expense.AmountCents, ids.Select(_ => 1L).ToList());
            var offset = expense.Id % ids.Count;
            var result = new Dictionary<int, long>();
            for (var i = 0; i < ids.Count; i++)
                result[ids[(i + offset) % ids.Count]] = shares[i];
            return result;
        }

        /// <summary>
        /// Paid, handed in and share per member, then the pot's line. An expense is shared between the members it doesn't
        /// exclude; <paramref name="exclusions"/> maps an expense ID to the member IDs left out. Settle-up payments made
        /// move the payer's balance up and the receiver's down, so what is still to settle shrinks.
        /// </summary>
        public static List<PartyBalance> Balances(IReadOnlyList<Participant> members, IEnumerable<PartyExpense> expenses,
            IReadOnlyDictionary<int, HashSet<int>> exclusions, IEnumerable<PartyContribution> contributions,
            IEnumerable<PartyPayment>? payments = null)
        {
            var paid = members.ToDictionary(m => m.Id, _ => 0L);
            var handedIn = members.ToDictionary(m => m.Id, _ => 0L);
            var share = members.ToDictionary(m => m.Id, _ => 0L);
            long pot = 0;

            foreach (var expense in expenses)
            {
                if (expense.PaidByParticipantId is { } payer && paid.ContainsKey(payer))
                    paid[payer] += expense.AmountCents;
                else
                    pot -= expense.AmountCents;

                var left = exclusions.TryGetValue(expense.Id, out var excluded) ? excluded : [];
                foreach (var (id, cents) in ShareExpense(expense, members.Select(m => m.Id).Where(id => !left.Contains(id))))
                    share[id] += cents;
            }

            foreach (var contribution in contributions)
            {
                handedIn[contribution.ParticipantId] += contribution.AmountCents;
                pot += contribution.AmountCents;
            }

            var settled = members.ToDictionary(m => m.Id, _ => 0L);
            long potSettled = 0;
            foreach (var payment in payments ?? [])
            {
                if (payment.FromId == PotId) potSettled += payment.AmountCents; else settled[payment.FromId] += payment.AmountCents;
                if (payment.ToId == PotId) potSettled -= payment.AmountCents; else settled[payment.ToId] -= payment.AmountCents;
            }

            var lines = members.Select(m => new PartyBalance(m.Id, m.Name, paid[m.Id], handedIn[m.Id], share[m.Id], settled[m.Id])).ToList();
            // The pot holds what was handed in less what it paid; its line is the opposite so that the lines sum to zero
            lines.Add(new PartyBalance(PotId, string.Empty, PaidCents: 0, ContributedCents: -pot, ShareCents: 0, potSettled));
            return lines;
        }

        /// <summary>
        /// Payments that bring every balance to zero: the largest debtor pays the largest creditor, until none is left
        /// (ties: lower ID first, so the result never changes between runs).
        /// </summary>
        /// <exception cref="ArgumentException">The balances don't add up to zero.</exception>
        public static List<SettlePayment> SettleUp(IEnumerable<PartyBalance> balances)
        {
            var open = balances.Where(b => b.BalanceCents != 0).ToDictionary(b => b.ParticipantId, b => b.BalanceCents);
            if (open.Values.Sum() != 0)
                throw new ArgumentException("Balances must add up to zero.", nameof(balances));

            var payments = new List<SettlePayment>();
            while (open.Count > 0)
            {
                var debtor = open.Where(o => o.Value < 0).OrderBy(o => o.Value).ThenBy(o => o.Key).First();
                var creditor = open.Where(o => o.Value > 0).OrderByDescending(o => o.Value).ThenBy(o => o.Key).First();
                var amount = Math.Min(-debtor.Value, creditor.Value);
                payments.Add(new SettlePayment(debtor.Key, creditor.Key, amount));
                Apply(open, debtor.Key, amount);
                Apply(open, creditor.Key, -amount);
            }

            return payments;
        }

        private static void Apply(Dictionary<int, long> open, int id, long delta)
        {
            if ((open[id] += delta) == 0)
                open.Remove(id);
        }
    }
}
