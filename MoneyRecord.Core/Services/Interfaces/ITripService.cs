using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>Trips: the active trip, its spending, the trip categories, and ending / reopening a trip.</summary>
    public interface ITripService
    {
        /// <summary>The active trip; null = none.</summary>
        Task<Trip?> GetActiveAsync();

        /// <summary>Every trip, the active one first, then the latest.</summary>
        Task<List<Trip>> GetAllAsync();

        Task<Trip?> GetAsync(int tripId);

        /// <summary>Creates (Id 0) or updates a trip with its per-trip-category limits (trip category ID → limit in the trip's currency).</summary>
        /// <exception cref="InvalidOperationException">A new trip while another one is active.</exception>
        Task<int> SaveAsync(Trip trip, IReadOnlyDictionary<int, decimal> limits);

        Task<Dictionary<int, decimal>> GetLimitsAsync(int tripId);

        Task<TripSummary> GetSummaryAsync(int tripId);

        /// <summary>The trip's expenses, newest first.</summary>
        Task<List<Transaction>> GetExpensesAsync(int tripId);

        /// <summary>
        /// Ends the trip: each of its expenses goes to the real category chosen for its trip category (trip category ID →
        /// category ID; every used trip category needs one). The choice is remembered for the next trip.
        /// </summary>
        Task EndAsync(int tripId, IReadOnlyDictionary<int, int> categoryByTripCategory);

        /// <summary>Makes an ended trip active again; its expenses go back to "Travel". Refused while another is active.</summary>
        Task ReopenAsync(int tripId);

        /// <summary>
        /// Deletes the trip. An active trip with expenses can't be (end it first); on an ended one the expenses stay in the
        /// ledger in their real categories.
        /// </summary>
        Task DeleteAsync(int tripId);

        /// <summary>The "Travel" expense category, created when there is none.</summary>
        Task<int> GetTravelCategoryIdAsync();

        Task<List<TripCategory>> GetCategoriesAsync();

        Task SaveCategoryAsync(TripCategory category);

        /// <summary>How many expenses use the trip category.</summary>
        Task<int> CountCategoryUsesAsync(int tripCategoryId);

        /// <summary>Deletes a trip category; its expenses and limits move to <paramref name="replacementId"/> (required when in use).</summary>
        Task DeleteCategoryAsync(int tripCategoryId, int? replacementId);
    }
}
