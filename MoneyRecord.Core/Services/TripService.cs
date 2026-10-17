using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;

namespace MoneyRecord.Services
{
    public sealed class TripService(
        DatabaseInitializer dbInitializer,
        IAccountRepository accounts,
        ICategoryRepository categories,
        IExchangeRateService exchangeRates,
        TimeProvider time) : ITripService
    {
        public const string TravelIconCode = "F034E";

        public async Task<Trip?> GetActiveAsync()
        {
            var db = await dbInitializer.GetConnectionAsync();
            return await db.Table<Trip>().Where(t => t.EndedDate == null).FirstOrDefaultAsync();
        }

        public async Task<List<Trip>> GetAllAsync()
        {
            var db = await dbInitializer.GetConnectionAsync();
            return (await db.Table<Trip>().ToListAsync())
                .OrderBy(t => t.IsActive ? 0 : 1)
                .ThenByDescending(t => t.StartDate)
                .ToList();
        }

        public async Task<Trip?> GetAsync(int tripId)
        {
            var db = await dbInitializer.GetConnectionAsync();
            return await db.FindAsync<Trip>(tripId);
        }

        public async Task<int> SaveAsync(Trip trip, IReadOnlyDictionary<int, decimal> limits)
        {
            trip.Name = trip.Name.Trim();
            if (trip.Name.Length == 0)
                throw new ArgumentException("A trip needs a name.", nameof(trip));
            if (trip.EndDate is { } end && end.Date < trip.StartDate.Date)
                throw new ArgumentException("A trip can't end before it starts.", nameof(trip));

            await dbInitializer.RunInTransactionAsync(db =>
            {
                if (trip.Id == 0)
                {
                    if (db.Table<Trip>().Any(t => t.EndedDate == null))
                        throw new InvalidOperationException("Another trip is active.");
                    trip.CreatedDate = time.GetLocalNow().DateTime;
                    db.Insert(trip);
                }
                else
                {
                    db.Update(trip);
                }

                db.Execute("DELETE FROM \"TripBudget\" WHERE TripId = ?", trip.Id);
                foreach (var (tripCategoryId, limit) in limits.Where(l => l.Value > 0))
                    db.Insert(new TripBudget { TripId = trip.Id, TripCategoryId = tripCategoryId, LimitCents = Money.ToCents(limit) });
            });
            return trip.Id;
        }

        public async Task<Dictionary<int, decimal>> GetLimitsAsync(int tripId)
        {
            var db = await dbInitializer.GetConnectionAsync();
            return (await db.Table<TripBudget>().Where(b => b.TripId == tripId).ToListAsync())
                .ToDictionary(b => b.TripCategoryId, b => Money.FromCents(b.LimitCents));
        }

        public async Task<List<Transaction>> GetExpensesAsync(int tripId)
        {
            var db = await dbInitializer.GetConnectionAsync();
            return (await db.Table<Transaction>().Where(t => t.TripId == tripId).ToListAsync())
                .OrderByDescending(t => t.Date).ThenByDescending(t => t.Id).ToList();
        }

        public async Task<TripSummary> GetSummaryAsync(int tripId)
        {
            var trip = await GetAsync(tripId) ?? throw new InvalidOperationException($"Trip {tripId} does not exist.");
            var code = MoneyFormatter.ResolveCurrencyCode(trip.CurrencyCode);
            var expenses = await GetExpensesAsync(tripId);
            var accountCurrencies = (await accounts.GetAllAsync())
                .ToDictionary(a => a.Id, a => MoneyFormatter.ResolveCurrencyCode(a.CurrencyCode));
            var converter = await exchangeRates.GetConverterAsync();
            var tripCategories = await GetCategoriesAsync();
            var limits = await GetLimitsAsync(tripId);

            var spentByCategory = new Dictionary<int, decimal>();
            var notConverted = new List<CurrencyAmount>();
            decimal spent = 0;
            foreach (var expense in expenses)
            {
                var own = expense.AccountId is { } id && accountCurrencies.TryGetValue(id, out var c) ? c : MoneyFormatter.BaseCurrency;
                var amount = string.Equals(own, code, StringComparison.OrdinalIgnoreCase)
                    ? Math.Abs(expense.Amount)
                    : converter.Convert(expense, own, code);
                if (amount is null)
                {
                    notConverted.Add(new CurrencyAmount(own, Math.Abs(expense.Amount)));
                    continue;
                }

                spent += amount.Value;
                if (expense.TripCategoryId is { } tc)
                    spentByCategory[tc] = spentByCategory.GetValueOrDefault(tc) + amount.Value;
            }

            var rows = tripCategories
                .Where(tc => spentByCategory.ContainsKey(tc.Id) || limits.ContainsKey(tc.Id))
                .Select(tc => new TripCategorySpent(tc, spentByCategory.GetValueOrDefault(tc.Id), limits.TryGetValue(tc.Id, out var l) ? l : null))
                .OrderByDescending(r => r.Spent)
                .ToList();
            return new TripSummary(trip, code, spent, expenses.Count, rows, CurrencyAmount.Totals(notConverted, MoneyFormatter.BaseCurrency));
        }

        public async Task EndAsync(int tripId, IReadOnlyDictionary<int, int> categoryByTripCategory)
        {
            try
            {
                await dbInitializer.RunInTransactionAsync(db =>
                {
                    var trip = db.Find<Trip>(tripId) ?? throw new InvalidOperationException($"Trip {tripId} does not exist.");
                    if (!trip.IsActive)
                        throw new InvalidOperationException("The trip has already ended.");

                    var used = db.Table<Transaction>().Where(t => t.TripId == tripId).ToList()
                        .Select(t => t.TripCategoryId).Distinct().ToList();
                    foreach (var tripCategoryId in used)
                    {
                        // An expense without a trip category has nothing to map: it stays where it is
                        if (tripCategoryId is not { } id)
                            continue;
                        if (!categoryByTripCategory.TryGetValue(id, out var categoryId) || db.Find<Category>(categoryId) is null)
                            throw new InvalidOperationException($"Trip category {id} has no category to move to.");

                        db.Execute("UPDATE \"Transaction\" SET CategoryId = ? WHERE TripId = ? AND TripCategoryId = ?", categoryId, tripId, id);
                        db.Execute("UPDATE \"TripCategory\" SET LastCategoryId = ? WHERE Id = ?", categoryId, id);
                    }

                    trip.EndedDate = time.GetLocalNow().DateTime;
                    db.Update(trip);
                });
            }
            finally
            {
                categories.InvalidateCache();
            }
        }

        public async Task ReopenAsync(int tripId)
        {
            var travelId = await GetTravelCategoryIdAsync();
            try
            {
                await dbInitializer.RunInTransactionAsync(db =>
                {
                    var trip = db.Find<Trip>(tripId) ?? throw new InvalidOperationException($"Trip {tripId} does not exist.");
                    if (trip.IsActive)
                        return;
                    if (db.Table<Trip>().Any(t => t.EndedDate == null))
                        throw new InvalidOperationException("Another trip is active.");

                    db.Execute("UPDATE \"Transaction\" SET CategoryId = ? WHERE TripId = ?", travelId, tripId);
                    trip.EndedDate = null;
                    db.Update(trip);
                });
            }
            finally
            {
                categories.InvalidateCache();
            }
        }

        public async Task DeleteAsync(int tripId)
        {
            await dbInitializer.RunInTransactionAsync(db =>
            {
                var trip = db.Find<Trip>(tripId);
                if (trip is null)
                    return;
                if (trip.IsActive && db.Table<Transaction>().Any(t => t.TripId == tripId))
                    throw new InvalidOperationException("End the trip before deleting it.");

                db.Execute("UPDATE \"Transaction\" SET TripId = NULL, TripCategoryId = NULL WHERE TripId = ?", tripId);
                db.Execute("DELETE FROM \"TripBudget\" WHERE TripId = ?", tripId);
                db.Delete<Trip>(tripId);
            });
        }

        public async Task<int> GetTravelCategoryIdAsync()
        {
            var db = await dbInitializer.GetConnectionAsync();
            var existing = await db.Table<Category>().Where(c => c.IsTravel).FirstOrDefaultAsync();
            if (existing != null)
                return existing.Id;

            var category = new Category
            {
                Name = AppResources.TravelCategory, Type = CategoryType.Expense, IconCode = TravelIconCode, IsTravel = true
            };
            await db.InsertAsync(category);
            categories.InvalidateCache();
            return category.Id;
        }

        public async Task<List<TripCategory>> GetCategoriesAsync()
        {
            var db = await dbInitializer.GetConnectionAsync();
            return (await db.Table<TripCategory>().ToListAsync()).OrderBy(c => c.Id).ToList();
        }

        public async Task SaveCategoryAsync(TripCategory category)
        {
            category.Name = category.Name.Trim();
            if (category.Name.Length == 0)
                throw new ArgumentException("A trip category needs a name.", nameof(category));
            var db = await dbInitializer.GetConnectionAsync();
            if (category.Id == 0)
                await db.InsertAsync(category);
            else
                await db.UpdateAsync(category);
        }

        public async Task<int> CountCategoryUsesAsync(int tripCategoryId)
        {
            var db = await dbInitializer.GetConnectionAsync();
            return await db.Table<Transaction>().CountAsync(t => t.TripCategoryId == tripCategoryId);
        }

        public async Task DeleteCategoryAsync(int tripCategoryId, int? replacementId)
        {
            await dbInitializer.RunInTransactionAsync(db =>
            {
                var uses = db.Table<Transaction>().Count(t => t.TripCategoryId == tripCategoryId);
                var limits = db.Table<TripBudget>().Where(b => b.TripCategoryId == tripCategoryId).ToList();
                if ((uses > 0 || limits.Count > 0) && (replacementId is null || replacementId == tripCategoryId))
                    throw new InvalidOperationException("The trip category is in use: choose a replacement.");

                if (replacementId is { } newId)
                {
                    db.Execute("UPDATE \"Transaction\" SET TripCategoryId = ? WHERE TripCategoryId = ?", newId, tripCategoryId);
                    foreach (var limit in limits)
                    {
                        // One limit per trip and category: the replacement keeps its own, otherwise it takes this one
                        if (db.Table<TripBudget>().Any(b => b.TripId == limit.TripId && b.TripCategoryId == newId))
                        {
                            db.Delete(limit);
                        }
                        else
                        {
                            limit.TripCategoryId = newId;
                            db.Update(limit);
                        }
                    }
                }

                db.Delete<TripCategory>(tripCategoryId);
            });
        }
    }
}
