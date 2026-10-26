using MoneyRecord.Helpers;
using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Models
{
    /// <summary>One day of a challenge: planned or done amount.</summary>
    public sealed record ChallengeDayInfo(int DayNumber, DateTime Date, long AmountCents, bool IsDone, DateTime? DoneDate, int? TransferId);

    /// <summary>A challenge with its days, for display.</summary>
    public sealed record ChallengeSummary(SavingsChallenge Challenge, IReadOnlyList<ChallengeDayInfo> Days, long SavedCents, long TotalCents, int DaysLeft)
    {
        public string Name => Challenge.Name;

        public bool IsFinished => Days.All(d => d.IsDone);

        /// <summary>"$120.00 saved of $465.00".</summary>
        public string SavedOfText => string.Format(AppResources.ChallengeSavedOf, Format(SavedCents), Format(TotalCents));

        /// <summary>"12 days left", or "Finished" when every day is done.</summary>
        public string DaysLeftText => IsFinished ? AppResources.ChallengeFinished : Plurals.ChallengeDaysLeft(DaysLeft);

        private string Format(long cents) => MoneyFormatter.Format(Money.FromCents(cents), Challenge.CurrencyCode);

        public int DoneCount => Days.Count(d => d.IsDone);

        /// <summary>Saved over the plan's total, 0 to 1.</summary>
        public double Fraction => TotalCents <= 0 ? 0 : Math.Min(1, (double)SavedCents / TotalCents);
    }
}
