using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;

namespace MoneyRecord.Services
{
    public sealed class PartyService(DatabaseInitializer dbInitializer, TimeProvider time) : IPartyService
    {
        public async Task<List<Participant>> GetParticipantsAsync()
        {
            var db = await dbInitializer.GetConnectionAsync();
            return (await db.Table<Participant>().ToListAsync()).OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public async Task<Participant> AddParticipantAsync(string name)
        {
            name = PayeeNames.Normalize(name);
            if (name.Length == 0)
                throw new ArgumentException("A friend needs a name.", nameof(name));

            var db = await dbInitializer.GetConnectionAsync();
            var existing = (await db.Table<Participant>().ToListAsync()).FirstOrDefault(p => PayeeNames.Same(p.Name, name));
            if (existing != null)
                return existing;

            var participant = new Participant { Name = name };
            await db.InsertAsync(participant);
            return participant;
        }

        public async Task RenameParticipantAsync(int participantId, string name)
        {
            name = PayeeNames.Normalize(name);
            if (name.Length == 0)
                throw new ArgumentException("A friend needs a name.", nameof(name));

            var db = await dbInitializer.GetConnectionAsync();
            var all = await db.Table<Participant>().ToListAsync();
            if (all.Any(p => p.Id != participantId && PayeeNames.Same(p.Name, name)))
                throw new PartyProblemException(PartyProblem.NameTaken, "Another friend has this name.");

            var participant = all.FirstOrDefault(p => p.Id == participantId)
                ?? throw new InvalidOperationException("The friend no longer exists.");
            participant.Name = name;
            await db.UpdateAsync(participant);
        }

        public async Task DeleteParticipantAsync(int participantId)
        {
            var db = await dbInitializer.GetConnectionAsync();
            if (await db.Table<PartyMember>().Where(m => m.ParticipantId == participantId).CountAsync() > 0)
                throw new PartyProblemException(PartyProblem.FriendInParty, "The friend is in a party.");
            await db.DeleteAsync<Participant>(participantId);
        }

        public async Task<List<PartyEvent>> GetPartiesAsync()
        {
            var db = await dbInitializer.GetConnectionAsync();
            return (await db.Table<PartyEvent>().ToListAsync()).OrderByDescending(p => p.Date).ThenByDescending(p => p.Id).ToList();
        }

        public async Task<PartyEvent?> GetAsync(int partyId)
        {
            var db = await dbInitializer.GetConnectionAsync();
            return await db.FindAsync<PartyEvent>(partyId);
        }

        public async Task<int> SaveAsync(PartyEvent party, IReadOnlyCollection<int> memberIds)
        {
            party.Name = PayeeNames.Normalize(party.Name);
            if (party.Name.Length == 0)
                throw new ArgumentException("A party needs a name.", nameof(party));
            if (party.BudgetCents is <= 0)
                throw new ArgumentException("A budget must be positive.", nameof(party));
            var members = memberIds.Distinct().ToList();
            if (members.Count == 0)
                throw new ArgumentException("A party needs at least one person.", nameof(memberIds));

            await dbInitializer.RunInTransactionAsync(db =>
            {
                if (party.Id == 0)
                {
                    party.CreatedDate = time.GetLocalNow().DateTime;
                    db.Insert(party);
                }
                else
                {
                    var used = db.Table<PartyExpense>().Where(e => e.PartyId == party.Id).ToList().Select(e => e.PaidByParticipantId)
                        .Concat(db.Table<PartyContribution>().Where(c => c.PartyId == party.Id).ToList().Select(c => (int?)c.ParticipantId))
                        .OfType<int>().ToHashSet();
                    if (used.Any(id => !members.Contains(id)))
                        throw new PartyProblemException(PartyProblem.MemberInUse, "A person with expenses or contributions can't be taken out.");

                    db.Update(party);
                    db.Execute("DELETE FROM \"PartyMember\" WHERE PartyId = ?", party.Id);
                    var left = db.Query<PartyExpenseExclusion>(
                        "SELECT x.* FROM \"PartyExpenseExclusion\" x JOIN \"PartyExpense\" e ON e.Id = x.ExpenseId WHERE e.PartyId = ?", party.Id)
                        .GroupBy(x => x.ExpenseId).ToDictionary(g => g.Key, g => g.Select(x => x.ParticipantId).ToHashSet());
                    var kept = db.Table<PartyExpense>().Where(e => e.PartyId == party.Id).ToList().Select(e => e.Id).ToList();
                    // An expense nobody remaining shares can't be summed
                    if (kept.Any(id => left.TryGetValue(id, out var excluded) && members.All(excluded.Contains)))
                        throw new PartyProblemException(PartyProblem.NobodyShares, "Taking this person out would leave an expense with nobody to share it.");

                    foreach (var expenseId in kept)
                        db.Execute("DELETE FROM \"PartyExpenseExclusion\" WHERE ExpenseId = ? AND ParticipantId NOT IN (" +
                            string.Join(",", members) + ")", expenseId);
                }

                foreach (var id in members)
                    db.Insert(new PartyMember { PartyId = party.Id, ParticipantId = id });
            });
            return party.Id;
        }

        public async Task DeleteAsync(int partyId)
        {
            await dbInitializer.RunInTransactionAsync(db =>
            {
                db.Execute("DELETE FROM \"PartyExpenseExclusion\" WHERE ExpenseId IN (SELECT Id FROM \"PartyExpense\" WHERE PartyId = ?)", partyId);
                db.Execute("DELETE FROM \"PartyExpense\" WHERE PartyId = ?", partyId);
                db.Execute("DELETE FROM \"PartyContribution\" WHERE PartyId = ?", partyId);
                db.Execute("DELETE FROM \"PartyMember\" WHERE PartyId = ?", partyId);
                db.Delete<PartyEvent>(partyId);
            });
        }

        public async Task<List<Participant>> GetMembersAsync(int partyId)
        {
            var db = await dbInitializer.GetConnectionAsync();
            var ids = (await db.Table<PartyMember>().Where(m => m.PartyId == partyId).ToListAsync()).Select(m => m.ParticipantId).ToHashSet();
            return (await db.Table<Participant>().ToListAsync()).Where(p => ids.Contains(p.Id)).OrderBy(p => p.Id).ToList();
        }

        public async Task<List<PartyExpense>> GetExpensesAsync(int partyId)
        {
            var db = await dbInitializer.GetConnectionAsync();
            return (await db.Table<PartyExpense>().Where(e => e.PartyId == partyId).ToListAsync())
                .OrderByDescending(e => e.Date).ThenByDescending(e => e.Id).ToList();
        }

        public async Task<Dictionary<int, HashSet<int>>> GetExclusionsAsync(int partyId)
        {
            var db = await dbInitializer.GetConnectionAsync();
            var rows = await db.QueryAsync<PartyExpenseExclusion>(
                "SELECT x.* FROM \"PartyExpenseExclusion\" x JOIN \"PartyExpense\" e ON e.Id = x.ExpenseId WHERE e.PartyId = ?", partyId);
            return rows.GroupBy(r => r.ExpenseId).ToDictionary(g => g.Key, g => g.Select(r => r.ParticipantId).ToHashSet());
        }

        public async Task<int> SaveExpenseAsync(PartyExpense expense, IReadOnlyCollection<int> excludedIds)
        {
            if (expense.AmountCents <= 0)
                throw new ArgumentException("An expense needs a positive amount.", nameof(expense));
            expense.Description = PayeeNames.Normalize(expense.Description);

            var members = (await GetMembersAsync(expense.PartyId)).Select(m => m.Id).ToHashSet();
            var excluded = excludedIds.Distinct().ToList();
            if (expense.PaidByParticipantId is { } payer && !members.Contains(payer))
                throw new ArgumentException("The payer isn't in the party.", nameof(expense));
            if (excluded.Any(id => !members.Contains(id)))
                throw new ArgumentException("Only people in the party can be left out.", nameof(excludedIds));
            if (members.All(excluded.Contains))
                throw new ArgumentException("Somebody has to share the expense.", nameof(excludedIds));

            await dbInitializer.RunInTransactionAsync(db =>
            {
                if (expense.Id == 0)
                    db.Insert(expense);
                else
                    db.Update(expense);

                db.Execute("DELETE FROM \"PartyExpenseExclusion\" WHERE ExpenseId = ?", expense.Id);
                foreach (var id in excluded)
                    db.Insert(new PartyExpenseExclusion { ExpenseId = expense.Id, ParticipantId = id });
            });
            return expense.Id;
        }

        public async Task DeleteExpenseAsync(int expenseId)
        {
            await dbInitializer.RunInTransactionAsync(db =>
            {
                db.Execute("DELETE FROM \"PartyExpenseExclusion\" WHERE ExpenseId = ?", expenseId);
                db.Delete<PartyExpense>(expenseId);
            });
        }

        public async Task<List<PartyContribution>> GetContributionsAsync(int partyId)
        {
            var db = await dbInitializer.GetConnectionAsync();
            return (await db.Table<PartyContribution>().Where(c => c.PartyId == partyId).ToListAsync())
                .OrderByDescending(c => c.Date).ThenByDescending(c => c.Id).ToList();
        }

        public async Task<int> SaveContributionAsync(PartyContribution contribution)
        {
            if (contribution.AmountCents <= 0)
                throw new ArgumentException("A contribution needs a positive amount.", nameof(contribution));
            if (!(await GetMembersAsync(contribution.PartyId)).Any(m => m.Id == contribution.ParticipantId))
                throw new ArgumentException("The contributor isn't in the party.", nameof(contribution));

            var db = await dbInitializer.GetConnectionAsync();
            if (contribution.Id == 0)
                await db.InsertAsync(contribution);
            else
                await db.UpdateAsync(contribution);
            return contribution.Id;
        }

        public async Task DeleteContributionAsync(int contributionId)
        {
            var db = await dbInitializer.GetConnectionAsync();
            await db.DeleteAsync<PartyContribution>(contributionId);
        }

        public async Task<PartySummary> GetSummaryAsync(int partyId)
        {
            var party = await GetAsync(partyId) ?? throw new InvalidOperationException("The party no longer exists.");
            var members = await GetMembersAsync(partyId);
            var expenses = await GetExpensesAsync(partyId);
            var balances = PartyLedger.Balances(members, expenses, await GetExclusionsAsync(partyId), await GetContributionsAsync(partyId));
            return new PartySummary(party, expenses.Sum(e => e.AmountCents), balances, PartyLedger.SettleUp(balances));
        }
    }
}
