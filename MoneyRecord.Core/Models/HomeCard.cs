namespace MoneyRecord.Models
{
    /// <summary>A card on Home that the user can reorder or hide (Settings → Home screen).</summary>
    public enum HomeCard
    {
        Balance,
        PeriodSummary,
        Recent,
        Upcoming,
        Budgets,
        Trip,
        Goals,
        Debts,

        /// <summary>The first-run checklist; on a new install it sits right under the balance (see <see cref="HomeLayout.Default"/>).</summary>
        GetStarted
    }
}
