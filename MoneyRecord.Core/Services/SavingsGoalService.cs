using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;

namespace MoneyRecord.Services
{
    public sealed class SavingsGoalService : ISavingsGoalService
    {
        private readonly DatabaseInitializer _dbInitializer;
        private readonly IAccountRepository _accountRepository;
        private readonly IBalanceService _balanceService;
        private readonly IPocketService _pockets;
        private readonly TimeProvider _time;

        public SavingsGoalService(DatabaseInitializer dbInitializer, IAccountRepository accountRepository,
            IBalanceService balanceService, IPocketService pockets, TimeProvider time)
        {
            _pockets = pockets;
            _dbInitializer = dbInitializer;
            _accountRepository = accountRepository;
            _balanceService = balanceService;
            _time = time;
        }

        public async Task<List<GoalProgress>> GetProgressAsync(IReadOnlyList<AccountBalanceInfo>? balances = null)
        {
            var db = await _dbInitializer.GetConnectionAsync();
            var goals = await db.Table<SavingsGoal>().ToListAsync();
            if (goals.Count == 0)
                return [];

            var links = await db.Table<SavingsGoalAccount>().ToListAsync();
            var pocketLinks = await db.Table<SavingsGoalPocket>().ToListAsync();
            var accounts = (await _accountRepository.GetAllAsync()).ToDictionary(a => a.Id);
            var pockets = pocketLinks.Count == 0
                ? new Dictionary<int, PocketSummary>()
                : (await _pockets.GetPocketsAsync()).ToDictionary(p => p.PocketId);
            var balanceOf = (balances ?? await _balanceService.GetAllAccountBalancesAsync())
                .ToDictionary(b => b.AccountId, b => b.CurrentBalance);
            var today = _time.GetLocalNow().Date;
            // The pace window usually starts on the same day for every goal: one query per distinct start
            var changesSince = new Dictionary<DateTime, Dictionary<int, decimal>>();
            var pocketChangesSince = new Dictionary<DateTime, Dictionary<int, long>>();

            var progress = new List<GoalProgress>();
            foreach (var goal in goals)
            {
                var linked = links.Where(l => l.GoalId == goal.Id)
                    .Select(l => accounts.GetValueOrDefault(l.AccountId))
                    .OfType<Account>()
                    .ToList();
                // An account whose currency changed after it was linked can't be added to the goal's amounts
                var counted = linked.Where(a => MoneyFormatter.SameCurrency(a.CurrencyCode, goal.CurrencyCode)).ToList();
                var uncounted = linked.Except(counted).ToList();
                // A pocket counts like an account: while its account is in the goal's currency
                var linkedPockets = pocketLinks.Where(l => l.GoalId == goal.Id)
                    .Select(l => pockets.GetValueOrDefault(l.PocketId))
                    .OfType<PocketSummary>()
                    .ToList();
                var countedPockets = linkedPockets.Where(p => MoneyFormatter.SameCurrency(p.Account.CurrencyCode, goal.CurrencyCode)).ToList();
                var uncountedPockets = linkedPockets.Except(countedPockets).ToList();
                var saved = counted.Sum(a => balanceOf.GetValueOrDefault(a.Id)) + countedPockets.Sum(p => p.Balance);

                decimal? pace = null;
                if (counted.Count + countedPockets.Count > 0)
                {
                    var start = GoalProjection.PaceStart(today,
                        counted.Select(a => a.CreatedDate).Concat(countedPockets.Select(p => p.Pocket.CreatedDate)).Min());
                    if (!changesSince.TryGetValue(start, out var changes))
                        changesSince[start] = changes = await _balanceService.GetChangesSinceAsync(start);
                    if (!pocketChangesSince.TryGetValue(start, out var pocketChanges))
                        pocketChangesSince[start] = pocketChanges = await _pockets.GetChangesSinceAsync(start);
                    var change = counted.Sum(a => changes.GetValueOrDefault(a.Id))
                        + countedPockets.Sum(p => Money.FromCents(pocketChanges.GetValueOrDefault(p.PocketId)));
                    pace = GoalProjection.MonthlyPace(change, start, today);
                }

                progress.Add(new GoalProgress(goal, counted, uncounted, saved, pace, today, countedPockets, uncountedPockets));
            }

            return progress
                .OrderBy(p => p.Status == GoalStatus.Reached)
                .ThenBy(p => p.Goal.TargetDate ?? DateTime.MaxValue)
                .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public async Task<Dictionary<int, int>> GetLinkedAccountsAsync()
        {
            var db = await _dbInitializer.GetConnectionAsync();
            return (await db.Table<SavingsGoalAccount>().ToListAsync()).ToDictionary(l => l.AccountId, l => l.GoalId);
        }

        public async Task<Dictionary<int, int>> GetLinkedPocketsAsync()
        {
            var db = await _dbInitializer.GetConnectionAsync();
            return (await db.Table<SavingsGoalPocket>().ToListAsync()).ToDictionary(l => l.PocketId, l => l.GoalId);
        }

        public Task SaveAsync(SavingsGoal goal, IReadOnlyCollection<int> accountIds, IReadOnlyCollection<int>? pocketIds = null) =>
            _dbInitializer.RunInTransactionAsync(db =>
            {
                // Null keeps the pockets the goal has
                pocketIds ??= goal.Id == 0
                    ? []
                    : db.Table<SavingsGoalPocket>().Where(l => l.GoalId == goal.Id).ToList().Select(l => l.PocketId).ToList();

                foreach (var pocketId in pocketIds)
                {
                    var pocket = db.Find<Pocket>(pocketId)
                        ?? throw new InvalidOperationException($"Pocket {pocketId} does not exist.");
                    var account = db.Find<Account>(pocket.AccountId)
                        ?? throw new InvalidOperationException($"Account {pocket.AccountId} does not exist.");
                    if (!MoneyFormatter.SameCurrency(account.CurrencyCode, goal.CurrencyCode))
                        throw new InvalidOperationException(
                            $"Pocket {pocketId} is in {MoneyFormatter.ResolveCurrencyCode(account.CurrencyCode)}, the goal in {goal.CurrencyCode}.");
                    // Its account's balance already holds the pocket's money
                    if (accountIds.Contains(pocket.AccountId))
                        throw new InvalidOperationException($"Pocket {pocketId} and its own account {pocket.AccountId} can't both be in a goal.");
                    var other = db.Table<SavingsGoalPocket>().FirstOrDefault(l => l.PocketId == pocketId && l.GoalId != goal.Id);
                    if (other != null)
                        throw new InvalidOperationException($"Pocket {pocketId} already belongs to goal {other.GoalId}.");
                }

                foreach (var accountId in accountIds)
                {
                    var account = db.Find<Account>(accountId)
                        ?? throw new InvalidOperationException($"Account {accountId} does not exist.");
                    if (!MoneyFormatter.SameCurrency(account.CurrencyCode, goal.CurrencyCode))
                        throw new InvalidOperationException(
                            $"Account {accountId} is in {MoneyFormatter.ResolveCurrencyCode(account.CurrencyCode)}, the goal in {goal.CurrencyCode}.");
                    var other = db.Table<SavingsGoalAccount>().FirstOrDefault(l => l.AccountId == accountId && l.GoalId != goal.Id);
                    if (other != null)
                        throw new InvalidOperationException($"Account {accountId} already belongs to goal {other.GoalId}.");
                }

                if (goal.Id == 0)
                    db.Insert(goal);
                else
                    db.Update(goal);

                db.Execute("DELETE FROM \"SavingsGoalAccount\" WHERE GoalId = ?", goal.Id);
                foreach (var accountId in accountIds.Distinct())
                    db.Insert(new SavingsGoalAccount { GoalId = goal.Id, AccountId = accountId });

                db.Execute("DELETE FROM \"SavingsGoalPocket\" WHERE GoalId = ?", goal.Id);
                foreach (var pocketId in pocketIds.Distinct())
                    db.Insert(new SavingsGoalPocket { GoalId = goal.Id, PocketId = pocketId });
            });

        public Task DeleteAsync(int goalId) =>
            _dbInitializer.RunInTransactionAsync(db =>
            {
                db.Execute("DELETE FROM \"SavingsGoalAccount\" WHERE GoalId = ?", goalId);
                db.Execute("DELETE FROM \"SavingsGoalPocket\" WHERE GoalId = ?", goalId);
                db.Delete<SavingsGoal>(goalId);
            });
    }
}
