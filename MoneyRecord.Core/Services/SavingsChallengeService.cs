using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;

namespace MoneyRecord.Services
{
    public sealed class SavingsChallengeService : ISavingsChallengeService
    {
        private readonly DatabaseInitializer _dbInitializer;
        private readonly TimeProvider _time;

        public SavingsChallengeService(DatabaseInitializer dbInitializer, TimeProvider time)
        {
            _dbInitializer = dbInitializer;
            _time = time;
        }

        public async Task<List<ChallengeSummary>> GetSummariesAsync()
        {
            var db = await _dbInitializer.GetConnectionAsync();
            var challenges = await db.Table<SavingsChallenge>().ToListAsync();
            var days = (await db.Table<SavingsChallengeDay>().ToListAsync()).ToLookup(d => d.ChallengeId);
            var today = _time.GetLocalNow().Date;
            return challenges.Select(c => Summarize(c, days[c.Id].ToList(), today))
                .OrderBy(s => s.IsFinished)
                .ThenByDescending(s => s.Challenge.CreatedDate)
                .ToList();
        }

        public async Task<ChallengeSummary?> GetSummaryAsync(int challengeId)
        {
            var db = await _dbInitializer.GetConnectionAsync();
            var challenge = await db.FindAsync<SavingsChallenge>(challengeId);
            if (challenge == null)
                return null;
            var days = await db.Table<SavingsChallengeDay>().Where(d => d.ChallengeId == challengeId).ToListAsync();
            return Summarize(challenge, days, _time.GetLocalNow().Date);
        }

        private static ChallengeSummary Summarize(SavingsChallenge challenge, List<SavingsChallengeDay> done, DateTime today)
        {
            var plan = ChallengePlan.Build(challenge.GoalCents, challenge.Days, challenge.Mode, challenge.RoundingCents);
            var doneByDay = done.ToDictionary(d => d.DayNumber);
            var days = new List<ChallengeDayInfo>(challenge.Days);
            for (var i = 1; i <= challenge.Days; i++)
            {
                var date = challenge.StartDate.Date.AddDays(i - 1);
                days.Add(doneByDay.TryGetValue(i, out var d)
                    ? new ChallengeDayInfo(i, date, d.AmountCents, true, d.DoneDate, d.TransferId)
                    : new ChallengeDayInfo(i, date, plan.DayCents[i - 1], false, null, null));
            }
            var saved = days.Where(d => d.IsDone).Sum(d => d.AmountCents);
            var lastDay = challenge.StartDate.Date.AddDays(challenge.Days - 1);
            var left = Math.Max(0, (lastDay - today).Days + 1);
            return new ChallengeSummary(challenge, days, saved, days.Sum(d => d.AmountCents), left);
        }

        public Task SaveAsync(SavingsChallenge challenge) =>
            _dbInitializer.RunInTransactionAsync(db =>
            {
                if (string.IsNullOrWhiteSpace(challenge.Name))
                    throw new InvalidOperationException("A challenge needs a name.");
                if (challenge.RoundingCents < 0)
                    throw new InvalidOperationException("The rounding can not be negative.");
                if (challenge.Days < 1)
                    throw new InvalidOperationException("A challenge needs at least one day.");
                if (challenge.GoalCents <= 0)
                    throw new InvalidOperationException("The goal must be more than zero.");
                if ((challenge.FromAccountId == null) != (challenge.ToAccountId == null))
                    throw new InvalidOperationException("Set both accounts or neither.");
                if (challenge.FromAccountId != null && challenge.FromAccountId == challenge.ToAccountId)
                    throw new InvalidOperationException("The two accounts must differ.");
                foreach (var accountId in new[] { challenge.FromAccountId, challenge.ToAccountId }.OfType<int>())
                {
                    var account = db.Find<Account>(accountId)
                        ?? throw new InvalidOperationException($"Account {accountId} does not exist.");
                    if (!MoneyFormatter.SameCurrency(account.CurrencyCode, challenge.CurrencyCode))
                        throw new InvalidOperationException(
                            $"Account {accountId} is in {MoneyFormatter.ResolveCurrencyCode(account.CurrencyCode)}, the challenge in {challenge.CurrencyCode}.");
                }

                if (challenge.Id == 0)
                {
                    db.Insert(challenge);
                    return;
                }

                var current = db.Find<SavingsChallenge>(challenge.Id)
                    ?? throw new InvalidOperationException($"Challenge {challenge.Id} does not exist.");
                var anyDone = db.Table<SavingsChallengeDay>().Count(d => d.ChallengeId == challenge.Id) > 0;
                if (anyDone && (current.GoalCents != challenge.GoalCents || current.Days != challenge.Days
                    || current.Mode != challenge.Mode || current.RoundingCents != challenge.RoundingCents
                    || !MoneyFormatter.SameCurrency(current.CurrencyCode, challenge.CurrencyCode)))
                    throw new InvalidOperationException("The plan can't change once a day is done.");
                db.Update(challenge);
            });

        public Task DeleteAsync(int challengeId) =>
            _dbInitializer.RunInTransactionAsync(db =>
            {
                db.Execute("DELETE FROM \"SavingsChallengeDay\" WHERE ChallengeId = ?", challengeId);
                db.Delete<SavingsChallenge>(challengeId);
            });

        public Task MarkDoneAsync(int challengeId, int dayNumber, bool recordTransfer) =>
            _dbInitializer.RunInTransactionAsync(db =>
            {
                var challenge = db.Find<SavingsChallenge>(challengeId)
                    ?? throw new InvalidOperationException($"Challenge {challengeId} does not exist.");
                if (dayNumber < 1 || dayNumber > challenge.Days)
                    throw new InvalidOperationException($"Day {dayNumber} is not in the challenge.");
                if (db.Table<SavingsChallengeDay>().Count(d => d.ChallengeId == challengeId && d.DayNumber == dayNumber) > 0)
                    throw new InvalidOperationException($"Day {dayNumber} is already done.");

                var amount = ChallengePlan.Build(challenge.GoalCents, challenge.Days, challenge.Mode, challenge.RoundingCents)
                    .DayCents[dayNumber - 1];
                var now = _time.GetLocalNow().DateTime;
                int? transferId = null;
                if (recordTransfer)
                {
                    if (!challenge.HasAccounts)
                        throw new InvalidOperationException("The challenge has no accounts to move money between.");
                    var from = db.Find<Account>(challenge.FromAccountId!.Value);
                    var to = db.Find<Account>(challenge.ToAccountId!.Value);
                    if (from == null || to == null)
                        throw new InvalidOperationException("An account of the challenge no longer exists.");
                    if (!MoneyFormatter.SameCurrency(from.CurrencyCode, challenge.CurrencyCode)
                        || !MoneyFormatter.SameCurrency(to.CurrencyCode, challenge.CurrencyCode))
                        throw new InvalidOperationException("An account of the challenge is in another currency.");
                    var transfer = new Transfer
                    {
                        Date = now, AmountCents = amount, DestinationAmountCents = amount,
                        SourceAccountId = from.Id, DestinationAccountId = to.Id, Description = challenge.Name
                    };
                    db.Insert(transfer);
                    transferId = transfer.Id;
                }
                db.Insert(new SavingsChallengeDay
                {
                    ChallengeId = challengeId, DayNumber = dayNumber, AmountCents = amount, DoneDate = now, TransferId = transferId
                });
            });

        public Task UnmarkAsync(int challengeId, int dayNumber, bool deleteTransfer) =>
            _dbInitializer.RunInTransactionAsync(db =>
            {
                var day = db.Table<SavingsChallengeDay>().FirstOrDefault(d => d.ChallengeId == challengeId && d.DayNumber == dayNumber)
                    ?? throw new InvalidOperationException($"Day {dayNumber} is not done.");
                db.Delete<SavingsChallengeDay>(day.Id);
                if (deleteTransfer && day.TransferId != null)
                    db.Delete<Transfer>(day.TransferId.Value);
            });
    }
}
