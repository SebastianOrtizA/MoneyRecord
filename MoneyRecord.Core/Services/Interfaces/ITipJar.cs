using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// Tips for the developer through the store's billing (the Google Play build only; every other build has
    /// <see cref="IsAvailable"/> = false and doesn't show the Support screen). A tip unlocks nothing.
    /// </summary>
    public interface ITipJar
    {
        bool IsAvailable { get; }

        /// <summary>The tips the store sells, cheapest first; empty when the store can't be reached (logged, never thrown).</summary>
        Task<IReadOnlyList<TipOption>> GetTipsAsync();

        /// <summary>Pays one tip and consumes it at once, so it can be given again.</summary>
        Task<TipOutcome> TipAsync(string productId);

        /// <summary>
        /// A tip paid after the sheet closed (a pending payment that settled) is consumed at the next start; the store
        /// refunds a purchase left unacknowledged after three days. Best-effort: a failure is logged, never thrown.
        /// </summary>
        Task ConsumePendingAsync();
    }
}
