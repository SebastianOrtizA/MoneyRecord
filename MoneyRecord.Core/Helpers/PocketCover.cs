namespace MoneyRecord.Helpers
{
    /// <summary>How much of an expense's shortfall a pocket covers when its money is taken out.</summary>
    public static class PocketCover
    {
        /// <summary>
        /// Takes the smaller of the shortfall and the pocket's balance; <c>StillShort</c> is what the next pocket
        /// has to cover.
        /// </summary>
        public static (decimal Take, decimal StillShort) Plan(decimal shortfall, decimal pocketBalance)
        {
            var take = Math.Max(0, Math.Min(shortfall, pocketBalance));
            return (take, Math.Max(0, shortfall - take));
        }
    }
}
