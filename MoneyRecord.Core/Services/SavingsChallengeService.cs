using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;
using SQLite;

namespace MoneyRecord.Services
{
    public sealed class SavingsChallengeService : ISavingsChallengeService
    {
        private readonly DatabaseInitializer _dbInitializer;
        private readonly IBalanceService _balances;
        private readonly TimeProvider _time;

        public SavingsChallengeService(DatabaseInitializer dbInitializer, IBalanceService balances, TimeProvider time)
        {
            _dbInitializer = dbInitializer;
            _balances = balances;
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
                    ? new ChallengeDayInfo(i, date, d.AmountCents, true, d.DoneDate,
                        d.TransferId != null || d.SourceMovementId != null || d.DestinationMovementId != null)
                    : new ChallengeDayInfo(i, date, plan.DayCents[i - 1], false, null, false));
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

                // A pocket decides its account
                if (challenge.ToPocketId is { } pocketId)
                {
                    var pocket = db.Find<Pocket>(pocketId)
                        ?? throw new InvalidOperationException($"Pocket {pocketId} does not exist.");
                    challenge.ToAccountId = pocket.AccountId;
                }
                if (challenge.ToAccountId is { } accountId)
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

        public async Task MarkDoneAsync(int challengeId, int dayNumber, ChallengePlace? source)
        {
            var db = await _dbInitializer.GetConnectionAsync();
            var challenge = await db.FindAsync<SavingsChallenge>(challengeId)
                ?? throw new InvalidOperationException($"Challenge {challengeId} does not exist.");
            if (dayNumber < 1 || dayNumber > challenge.Days)
                throw new InvalidOperationException($"Day {dayNumber} is not in the challenge.");

            var amount = ChallengePlan.Build(challenge.GoalCents, challenge.Days, challenge.Mode, challenge.RoundingCents)
                .DayCents[dayNumber - 1];
            var route = source == null ? null : await ResolveRouteAsync(db, challenge, source);
            // Read outside the transaction (it is async); what the pockets hold is read inside it, with the inserts
            var balanceCents = route is { NeedsTransfer: false, DestinationPocketId: not null }
                ? Money.ToCents(await _balances.GetAccountBalanceAsync(route.DestinationAccountId))
                : 0;
            var now = _time.GetLocalNow().DateTime;

            await _dbInitializer.RunInTransactionAsync(conn =>
            {
                if (conn.Table<SavingsChallengeDay>().Count(d => d.ChallengeId == challengeId && d.DayNumber == dayNumber) > 0)
                    throw new InvalidOperationException($"Day {dayNumber} is already done.");

                var day = new SavingsChallengeDay { ChallengeId = challengeId, DayNumber = dayNumber, AmountCents = amount, DoneDate = now };
                if (route != null)
                {
                    int? transferId = null;
                    if (route.NeedsTransfer)
                    {
                        var transfer = new Transfer
                        {
                            Date = now, AmountCents = amount, DestinationAmountCents = amount, Description = challenge.Name,
                            SourceAccountId = route.SourceAccountId, DestinationAccountId = route.DestinationAccountId
                        };
                        conn.Insert(transfer);
                        transferId = transfer.Id;
                        day.TransferId = transferId;
                    }

                    if (route.SourcePocketId is { } sourcePocket)
                    {
                        var held = conn.ExecuteScalar<long>(
                            "SELECT COALESCE(SUM(AmountCents), 0) FROM \"PocketMovement\" WHERE PocketId = ?", sourcePocket);
                        if (amount > held)
                            throw new PocketProblemException(PocketProblem.NotEnoughInPocket, $"Pocket {sourcePocket} holds less than that.");
                        var takeOut = new PocketMovement
                        {
                            PocketId = sourcePocket, AmountCents = -amount, Date = now, TransferId = transferId, Note = challenge.Name
                        };
                        conn.Insert(takeOut);
                        day.SourceMovementId = takeOut.Id;
                    }

                    if (route.DestinationPocketId is { } destinationPocket)
                    {
                        // Money that arrived by transfer is new; money set aside inside the account must be available there
                        if (!route.NeedsTransfer)
                            PocketService.EnsureAvailable(conn, route.DestinationAccountId, amount, balanceCents);
                        var setAside = new PocketMovement
                        {
                            PocketId = destinationPocket, AmountCents = amount, Date = now, TransferId = transferId, Note = challenge.Name
                        };
                        conn.Insert(setAside);
                        day.DestinationMovementId = setAside.Id;
                    }
                }
                conn.Insert(day);
            });
        }

        public Task UnmarkAsync(int challengeId, int dayNumber, bool undoMoney) =>
            _dbInitializer.RunInTransactionAsync(db =>
            {
                var day = db.Table<SavingsChallengeDay>().FirstOrDefault(d => d.ChallengeId == challengeId && d.DayNumber == dayNumber)
                    ?? throw new InvalidOperationException($"Day {dayNumber} is not done.");
                db.Delete<SavingsChallengeDay>(day.Id);
                if (!undoMoney)
                    return;
                // Each may be gone already (a pocket deleted, a transfer deleted on the Transfers page): deleting a missing row is a no-op
                if (day.SourceMovementId != null)
                    db.Delete<PocketMovement>(day.SourceMovementId.Value);
                if (day.DestinationMovementId != null)
                    db.Delete<PocketMovement>(day.DestinationMovementId.Value);
                if (day.TransferId != null)
                    db.Delete<Transfer>(day.TransferId.Value);
            });

        private sealed record Route(int SourceAccountId, int? SourcePocketId, int DestinationAccountId, int? DestinationPocketId)
        {
            public bool NeedsTransfer => SourceAccountId != DestinationAccountId;
        }

        private static async Task<Route> ResolveRouteAsync(SQLiteAsyncConnection db, SavingsChallenge challenge, ChallengePlace source)
        {
            if (challenge.ToAccountId is not { } destinationAccount)
                throw new InvalidOperationException("The challenge has no destination to move money to.");
            if ((source.AccountId == null) == (source.PocketId == null))
                throw new InvalidOperationException("The source is an account or a pocket.");

            int sourceAccountId;
            if (source.PocketId is { } pocketId)
            {
                var pocket = await db.FindAsync<Pocket>(pocketId)
                    ?? throw new PocketProblemException(PocketProblem.PocketNotFound, $"Pocket {pocketId} does not exist.");
                sourceAccountId = pocket.AccountId;
            }
            else
            {
                sourceAccountId = source.AccountId!.Value;
            }

            foreach (var accountId in new[] { sourceAccountId, destinationAccount }.Distinct())
            {
                var account = await db.FindAsync<Account>(accountId)
                    ?? throw new InvalidOperationException($"Account {accountId} does not exist.");
                if (!MoneyFormatter.SameCurrency(account.CurrencyCode, challenge.CurrencyCode))
                    throw new InvalidOperationException($"Account {accountId} is in another currency than the challenge.");
            }
            if (!ChallengeSources.IsValidRoute(sourceAccountId, source.PocketId, destinationAccount, challenge.ToPocketId))
                throw new InvalidOperationException("That source and the destination would not move any money.");
            return new Route(sourceAccountId, source.PocketId, destinationAccount, challenge.ToPocketId);
        }
    }
}
