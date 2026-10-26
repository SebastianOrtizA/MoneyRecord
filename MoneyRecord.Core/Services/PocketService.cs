using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;

namespace MoneyRecord.Services
{
    public sealed class PocketService(DatabaseInitializer dbInitializer, IBalanceService balanceService) : IPocketService
    {
        // A grouped total, read by sqlite-net
        private sealed class KeyedSum
        {
            public int RowKey { get; set; }

            public long Total { get; set; }
        }

        public async Task<Dictionary<int, long>> GetBalancesAsync()
        {
            var db = await dbInitializer.GetConnectionAsync();
            var rows = await db.QueryAsync<KeyedSum>(
                "SELECT PocketId AS RowKey, SUM(AmountCents) AS Total FROM \"PocketMovement\" GROUP BY PocketId");
            return rows.ToDictionary(r => r.RowKey, r => r.Total);
        }

        public async Task<Dictionary<int, long>> GetTotalsByAccountAsync()
        {
            var db = await dbInitializer.GetConnectionAsync();
            var rows = await db.QueryAsync<KeyedSum>(
                "SELECT p.AccountId AS RowKey, SUM(m.AmountCents) AS Total FROM \"PocketMovement\" m " +
                "JOIN \"Pocket\" p ON p.Id = m.PocketId GROUP BY p.AccountId");
            return rows.ToDictionary(r => r.RowKey, r => r.Total);
        }

        public async Task<List<PocketSummary>> GetPocketsAsync(int? accountId = null)
        {
            var db = await dbInitializer.GetConnectionAsync();
            var pockets = accountId is { } id
                ? await db.Table<Pocket>().Where(p => p.AccountId == id).ToListAsync()
                : await db.Table<Pocket>().ToListAsync();
            if (pockets.Count == 0)
                return [];

            var accounts = (await db.Table<Account>().ToListAsync()).ToDictionary(a => a.Id);
            var balances = await GetBalancesAsync();
            return pockets
                .Where(p => accounts.ContainsKey(p.AccountId))
                .Select(p => new PocketSummary(p, accounts[p.AccountId], balances.GetValueOrDefault(p.Id)))
                .OrderBy(s => s.Account.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(s => s.Pocket.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public async Task<Dictionary<int, long>> GetChangesSinceAsync(DateTime since)
        {
            var db = await dbInitializer.GetConnectionAsync();
            var rows = await db.QueryAsync<KeyedSum>(
                "SELECT PocketId AS RowKey, SUM(AmountCents) AS Total FROM \"PocketMovement\" WHERE Date >= ? GROUP BY PocketId", since);
            return rows.ToDictionary(r => r.RowKey, r => r.Total);
        }

        public async Task<List<PocketMovement>> GetMovementsAsync(int pocketId)
        {
            var db = await dbInitializer.GetConnectionAsync();
            return await db.Table<PocketMovement>().Where(m => m.PocketId == pocketId)
                .OrderByDescending(m => m.Date).ThenByDescending(m => m.Id).ToListAsync();
        }

        public Task SaveAsync(Pocket pocket) =>
            dbInitializer.RunInTransactionAsync(db =>
            {
                var account = db.Find<Account>(pocket.AccountId)
                    ?? throw new PocketProblemException(PocketProblem.AccountNotFound, $"Account {pocket.AccountId} does not exist.");
                if (account.IsDebt)
                    throw new PocketProblemException(PocketProblem.DebtAccount, "A debt's account can't have pockets.");

                if (pocket.Id == 0)
                {
                    db.Insert(pocket);
                    return;
                }

                // Moving it would skip the goal and available checks of its new account
                var existing = db.Find<Pocket>(pocket.Id)
                    ?? throw new PocketProblemException(PocketProblem.PocketNotFound, $"Pocket {pocket.Id} does not exist.");
                if (existing.AccountId != pocket.AccountId)
                    throw new PocketProblemException(PocketProblem.AccountNotFound, "A pocket can't change account.");
                db.Update(pocket);
            });

        public Task DeleteAsync(int pocketId) =>
            dbInitializer.RunInTransactionAsync(db =>
            {
                db.Execute("DELETE FROM \"PocketMovement\" WHERE PocketId = ?", pocketId);
                db.Execute("DELETE FROM \"SavingsGoalPocket\" WHERE PocketId = ?", pocketId);
                // A challenge that saves into the pocket goes back to only marking days
                db.Execute("UPDATE \"SavingsChallenge\" SET ToAccountId = NULL, ToPocketId = NULL WHERE ToPocketId = ?", pocketId);
                db.Delete<Pocket>(pocketId);
            });

        public async Task MoveAsync(int pocketId, decimal amount, DateTime date, string? note = null)
        {
            var cents = Money.ToCents(amount);
            if (cents == 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "A movement can't be zero.");

            var db = await dbInitializer.GetConnectionAsync();
            var pocket = await db.FindAsync<Pocket>(pocketId)
                ?? throw new PocketProblemException(PocketProblem.PocketNotFound, $"Pocket {pocketId} does not exist.");
            // Read outside the transaction (it is async); what the pockets hold is read inside it, with the insert
            var balanceCents = cents > 0 ? Money.ToCents(await balanceService.GetAccountBalanceAsync(pocket.AccountId)) : 0;

            await dbInitializer.RunInTransactionAsync(conn =>
            {
                if (cents > 0)
                {
                    // Available = the account's balance minus everything its pockets hold
                    var inPockets = conn.ExecuteScalar<long>(
                        "SELECT COALESCE(SUM(m.AmountCents), 0) FROM \"PocketMovement\" m JOIN \"Pocket\" p ON p.Id = m.PocketId WHERE p.AccountId = ?",
                        pocket.AccountId);
                    if (cents > balanceCents - inPockets)
                        throw new PocketProblemException(PocketProblem.NotEnoughAvailable,
                            $"Account {pocket.AccountId} has {balanceCents - inPockets} cents available.");
                }
                else
                {
                    var held = conn.ExecuteScalar<long>(
                        "SELECT COALESCE(SUM(AmountCents), 0) FROM \"PocketMovement\" WHERE PocketId = ?", pocketId);
                    if (-cents > held)
                        throw new PocketProblemException(PocketProblem.NotEnoughInPocket, $"Pocket {pocketId} holds less than that.");
                }

                conn.Insert(new PocketMovement { PocketId = pocketId, AmountCents = cents, Date = date, Note = note });
            });
        }

        public async Task<decimal> GetShortfallAsync(int accountId, decimal extraOutflow, DateTime date)
        {
            // A future-dated entry is in no balance before its date
            if (date >= balanceService.CountedBefore)
                return 0;

            var inPockets = (await GetTotalsByAccountAsync()).GetValueOrDefault(accountId);
            if (inPockets == 0)
                return 0;

            var balanceCents = Money.ToCents(await balanceService.GetAccountBalanceAsync(accountId));
            var shortfall = inPockets - (balanceCents - Money.ToCents(extraOutflow));
            return shortfall > 0 ? Money.FromCents(shortfall) : 0;
        }

        public Task CoverAsync(int accountId, IReadOnlyList<PocketTake> takes, DateTime date, int? transactionId = null, int? transferId = null) =>
            dbInitializer.RunInTransactionAsync(db =>
            {
                foreach (var take in takes.Where(t => t.Amount > 0))
                {
                    var cents = Money.ToCents(take.Amount);
                    var pocket = db.Find<Pocket>(take.PocketId)
                        ?? throw new PocketProblemException(PocketProblem.PocketNotFound, $"Pocket {take.PocketId} does not exist.");
                    if (pocket.AccountId != accountId)
                        throw new PocketProblemException(PocketProblem.PocketNotFound, $"Pocket {take.PocketId} is not in account {accountId}.");
                    var held = db.ExecuteScalar<long>(
                        "SELECT COALESCE(SUM(AmountCents), 0) FROM \"PocketMovement\" WHERE PocketId = ?", take.PocketId);
                    if (cents > held)
                        throw new PocketProblemException(PocketProblem.NotEnoughInPocket, $"Pocket {take.PocketId} holds less than that.");

                    db.Insert(new PocketMovement
                    {
                        PocketId = take.PocketId, AmountCents = -cents, Date = date,
                        TransactionId = transactionId, TransferId = transferId
                    });
                }
            });
    }
}
