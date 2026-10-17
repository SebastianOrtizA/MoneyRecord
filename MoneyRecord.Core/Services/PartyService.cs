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
                        .Concat(db.Table<PartyPayment>().Where(p => p.PartyId == party.Id).ToList().SelectMany(p => new int?[] { p.FromId, p.ToId }))
                        .OfType<int>().Where(id => id != PartyLedger.PotId).ToHashSet();
                    if (used.Any(id => !members.Contains(id)))
                        throw new PartyProblemException(PartyProblem.MemberInUse, "A person with expenses or contributions can't be taken out.");

                    // The caller edits a copy without the owner's share link: keep it (the owner stays only while a member)
                    var stored = db.Find<PartyEvent>(party.Id);
                    party.MeParticipantId = stored?.MeParticipantId is { } me && members.Contains(me) ? me : null;
                    party.OwnTransactionId = stored?.OwnTransactionId;
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
                db.Execute("DELETE FROM \"PartyPayment\" WHERE PartyId = ?", partyId);
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

        public async Task<List<PartyPayment>> GetPaymentsAsync(int partyId)
        {
            var db = await dbInitializer.GetConnectionAsync();
            return (await db.Table<PartyPayment>().Where(p => p.PartyId == partyId).ToListAsync())
                .OrderByDescending(p => p.Date).ThenByDescending(p => p.Id).ToList();
        }

        public async Task<int> SavePaymentAsync(PartyPayment payment)
        {
            if (payment.AmountCents <= 0)
                throw new ArgumentException("A payment needs a positive amount.", nameof(payment));
            if (payment.FromId == payment.ToId)
                throw new ArgumentException("A payment goes between two different people.", nameof(payment));
            var ids = (await GetMembersAsync(payment.PartyId)).Select(m => m.Id).Append(PartyLedger.PotId).ToHashSet();
            if (!ids.Contains(payment.FromId) || !ids.Contains(payment.ToId))
                throw new ArgumentException("A payment is between members of the party (or its pot).", nameof(payment));

            var db = await dbInitializer.GetConnectionAsync();
            if (payment.Id == 0)
                await db.InsertAsync(payment);
            else
                await db.UpdateAsync(payment);
            return payment.Id;
        }

        public async Task DeletePaymentAsync(int paymentId)
        {
            var db = await dbInitializer.GetConnectionAsync();
            await db.DeleteAsync<PartyPayment>(paymentId);
        }

        public async Task<Transaction> RecordOwnShareAsync(int partyId, int meParticipantId, int? accountId, int? categoryId)
        {
            var summary = await GetSummaryAsync(partyId);
            var share = summary.Balances.FirstOrDefault(b => b.ParticipantId == meParticipantId && meParticipantId != PartyLedger.PotId)
                ?? throw new ArgumentException("The owner isn't in the party.", nameof(meParticipantId));
            if (share.ShareCents <= 0)
                throw new PartyProblemException(PartyProblem.NoShare, "There is no share to record.");

            var db = await dbInitializer.GetConnectionAsync();
            Transaction? transaction = null;
            if (summary.Party.OwnTransactionId is { } existingId)
                transaction = await db.FindAsync<Transaction>(existingId);
            var isNew = transaction == null;
            transaction ??= new Transaction { Type = TransactionType.Expense };
            var chosenAccountId = accountId ?? transaction.AccountId ?? throw new ArgumentException("An account is needed.", nameof(accountId));
            var chosenCategoryId = categoryId ?? (isNew ? throw new ArgumentException("A category is needed.", nameof(categoryId)) : transaction.CategoryId);
            var account = await db.FindAsync<Account>(chosenAccountId) ?? throw new ArgumentException("The account no longer exists.", nameof(accountId));
            if (!MoneyFormatter.SameCurrency(account.CurrencyCode, summary.Party.CurrencyCode))
                throw new PartyProblemException(PartyProblem.AccountCurrency, "The account is in another currency than the party.");

            transaction.Date = summary.Party.Date;
            transaction.Description = summary.Party.Name;
            transaction.AmountCents = share.ShareCents;
            transaction.AccountId = chosenAccountId;
            transaction.CategoryId = chosenCategoryId;

            await dbInitializer.RunInTransactionAsync(conn =>
            {
                if (isNew)
                    conn.Insert(transaction);
                else
                    conn.Update(transaction);
                var party = conn.Get<PartyEvent>(partyId);
                party.MeParticipantId = meParticipantId;
                party.OwnTransactionId = transaction.Id;
                conn.Update(party);
            });
            return transaction;
        }

        public async Task<PartySummary> GetSummaryAsync(int partyId)
        {
            var party = await GetAsync(partyId) ?? throw new InvalidOperationException("The party no longer exists.");
            var members = await GetMembersAsync(partyId);
            var expenses = await GetExpensesAsync(partyId);
            var balances = PartyLedger.Balances(members, expenses, await GetExclusionsAsync(partyId),
                await GetContributionsAsync(partyId), await GetPaymentsAsync(partyId));
            return new PartySummary(party, expenses.Sum(e => e.AmountCents), balances, PartyLedger.SettleUp(balances));
        }
    }
}
