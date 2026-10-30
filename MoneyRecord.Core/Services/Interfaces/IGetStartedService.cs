using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>Home's first-run checklist: open until the user hides it or does everything on it.</summary>
    public interface IGetStartedService
    {
        /// <summary>
        /// The checklist, or null when it is hidden or complete. An install that already has transactions when this first runs
        /// (an update from before the checklist) never sees it.
        /// </summary>
        Task<GetStartedChecklist?> GetOpenAsync();

        /// <summary>Hides the checklist for good.</summary>
        void Dismiss();
    }
}
