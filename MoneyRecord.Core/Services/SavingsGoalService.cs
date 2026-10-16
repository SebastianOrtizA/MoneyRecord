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
        private readonly TimeProvider _time;

        public SavingsGoalService(DatabaseInitializer dbInitializer, IAccountRepository accountRepository,
            IBalanceService balanceService, TimeProvider time)
        {
            _dbInitializer = dbInitializer;
            _accountRepository = accountRepository;
            _balanceService = balanceService;
            _time = time;
        }

        public async Task<List<GoalProgress>> GetProgressAsync()
        {
            var db = await _dbInitializer.GetConnectionAsync();
            var goals = await db.Table<SavingsGoal>().ToListAsync();
            if (goals.Count == 0)
                return [];

            var links = await db.Table<SavingsGoalAccount>().ToListAsync();
            var accounts = (await _accountRepository.GetAllAsync()).ToDictionary(a => a.Id);
            var balances = (await _balanceService.GetAllAccountBalancesAsync()).ToDictionary(b => b.AccountId, b => b.CurrentBalance);
            var today = _time.GetLocalNow().Date;
            // The pace window usually starts on the same day for every goal: one query per distinct start
            var changesAfter = new Dictionary<DateTime, Dictionary<int, decimal>>();

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
                var saved = counted.Sum(a => balances.GetValueOrDefault(a.Id));

                decimal? pace = null;
                if (counted.Count > 0)
                {
                    var start = GoalProjection.PaceStart(today, counted.Min(a => a.CreatedDate));
                    if (!changesAfter.TryGetValue(start, out var changes))
                        changesAfter[start] = changes = await _balanceService.GetChangesAfterAsync(start);
                    pace = GoalProjection.MonthlyPace(counted.Sum(a => changes.GetValueOrDefault(a.Id)), start, today);
                }

                progress.Add(new GoalProgress(goal, counted, uncounted, saved, pace, today));
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

        public Task SaveAsync(SavingsGoal goal, IReadOnlyCollection<int> accountIds) =>
            _dbInitializer.RunInTransactionAsync(db =>
            {
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
            });

        public Task DeleteAsync(int goalId) =>
            _dbInitializer.RunInTransactionAsync(db =>
            {
                db.Execute("DELETE FROM \"SavingsGoalAccount\" WHERE GoalId = ?", goalId);
                db.Delete<SavingsGoal>(goalId);
            });
    }
}
