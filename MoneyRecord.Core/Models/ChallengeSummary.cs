namespace MoneyRecord.Models
{
    /// <summary>One day of a challenge: planned or done amount.</summary>
    public sealed record ChallengeDayInfo(int DayNumber, DateTime Date, long AmountCents, bool IsDone, DateTime? DoneDate, int? TransferId);

    /// <summary>A challenge with its days, for display.</summary>
    public sealed record ChallengeSummary(SavingsChallenge Challenge, IReadOnlyList<ChallengeDayInfo> Days, long SavedCents, long TotalCents, int DaysLeft)
    {
        public bool IsFinished => Days.All(d => d.IsDone);

        public int DoneCount => Days.Count(d => d.IsDone);

        /// <summary>Saved over the plan's total, 0 to 1.</summary>
        public double Fraction => TotalCents <= 0 ? 0 : Math.Min(1, (double)SavedCents / TotalCents);
    }
}
