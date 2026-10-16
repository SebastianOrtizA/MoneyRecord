using MoneyRecord.Helpers;
using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Models
{
    /// <summary>A goal's outlook (<see cref="GoalProjection.Status"/>).</summary>
    public enum GoalStatus
    {
        /// <summary>No target date, or too little history to tell.</summary>
        None,
        OnTrack,
        Behind,
        Reached
    }

    /// <summary>
    /// A goal as the Goals page and the Home card show it: saved = today's balance of its linked accounts in the goal's
    /// currency, plus the projection. Built once per load (texts in the current language).
    /// </summary>
    public sealed class GoalProgress
    {
        /// <param name="accounts">The linked accounts in the goal's currency (their balances are <paramref name="saved"/>).</param>
        /// <param name="uncounted">Linked accounts now in another currency: listed, not counted.</param>
        /// <param name="monthlyPace">The accounts' average monthly change (<see cref="GoalProjection.MonthlyPace"/>).</param>
        public GoalProgress(SavingsGoal goal, IReadOnlyList<Account> accounts, IReadOnlyList<Account> uncounted,
            decimal saved, decimal? monthlyPace, DateTime today)
        {
            Goal = goal;
            AccountIds = accounts.Concat(uncounted).Select(a => a.Id).ToList();
            AccountNames = string.Join(" · ", accounts.Select(a => a.Name));
            UncountedText = uncounted.Count == 0
                ? string.Empty
                : string.Format(AppResources.GoalAccountsNotCounted, string.Join(", ", uncounted.Select(a => a.Name)));
            Saved = saved;
            MonthlyPace = monthlyPace;

            var target = goal.TargetAmount;
            Remaining = Math.Max(0, target - saved);
            ProgressPercentage = target > 0 ? Math.Clamp((double)(saved / target) * 100, 0, 100) : 0;
            ProjectedDate = Remaining > 0 ? GoalProjection.ProjectedDate(Remaining, monthlyPace, today) : null;
            Status = GoalProjection.Status(Remaining, goal.TargetDate, monthlyPace, ProjectedDate, today);
            if (Status != GoalStatus.Reached && goal.TargetDate is { } date && date.Date >= today.Date)
                NeededPerMonth = GoalProjection.NeededPerMonth(Remaining, date, today);

            PlanText = PlanFor(goal.TargetDate);
            PaceText = Status == GoalStatus.Reached ? string.Empty : PaceFor();
        }

        public SavingsGoal Goal { get; }

        public int GoalId => Goal.Id;

        public string Name => Goal.Name;

        public string CurrencyCode => Goal.CurrencyCode;

        public decimal TargetAmount => Goal.TargetAmount;

        /// <summary>Every linked account, counted or not.</summary>
        public IReadOnlyList<int> AccountIds { get; }

        /// <summary>"Savings · Vacation fund": the counted accounts.</summary>
        public string AccountNames { get; }

        /// <summary>"Not counted (other currency): Euros" when a linked account changed currency; empty otherwise.</summary>
        public string UncountedText { get; }

        public bool HasUncounted => UncountedText.Length > 0;

        public decimal Saved { get; }

        public decimal Remaining { get; }

        /// <summary>0–100.</summary>
        public double ProgressPercentage { get; }

        public decimal? MonthlyPace { get; }

        /// <summary>The month when the pace reaches the target; null when it doesn't (or already reached).</summary>
        public DateTime? ProjectedDate { get; }

        /// <summary>Only with a target date that hasn't passed and the goal not reached.</summary>
        public decimal? NeededPerMonth { get; }

        public GoalStatus Status { get; }

        /// <summary>Behind shows amber; everything else green (the budget colours).</summary>
        public BudgetLevel Level => Status == GoalStatus.Behind ? BudgetLevel.Warning : BudgetLevel.OnTrack;

        public string StatusText => Status switch
        {
            GoalStatus.Reached => AppResources.GoalReached,
            GoalStatus.OnTrack => AppResources.OnTrack,
            GoalStatus.Behind => AppResources.GoalBehind,
            _ => string.Empty
        };

        public bool HasStatus => Status != GoalStatus.None;

        /// <summary>Trophy, check or alert: the status shape, so colour isn't the only signal.</summary>
        public string StatusIcon => IconHelper.GetDisplayIcon(Status switch
        {
            GoalStatus.Reached => "F0538",
            GoalStatus.Behind => "F0026",
            _ => "F05E0"
        }, "F05E0");

        /// <summary>"$1,200.00 of $5,000.00".</summary>
        public string SavedOfTargetText => string.Format(AppResources.GoalSavedOfTarget,
            MoneyFormatter.Format(Saved, CurrencyCode), MoneyFormatter.Format(TargetAmount, CurrencyCode));

        /// <summary>"Save $300.00 a month to reach it by Mar 1, 2027", or that the date has passed; empty without a date.</summary>
        public string PlanText { get; }

        public bool HasPlan => PlanText.Length > 0;

        /// <summary>"At your pace: Jun 2027", or why there's no projection; empty once reached.</summary>
        public string PaceText { get; }

        public bool HasPace => PaceText.Length > 0;

        public string DisplayIcon => IconHelper.GetDisplayIcon(SavingsGoalIcon, SavingsGoalIcon);

        /// <summary>Piggy bank.</summary>
        public const string SavingsGoalIcon = "F1007";

        private string PlanFor(DateTime? targetDate)
        {
            if (Status == GoalStatus.Reached || targetDate is not { } date)
                return string.Empty;

            var dateText = DateFormatter.Format(date, DateStyle.Medium);
            return NeededPerMonth is { } needed
                ? string.Format(AppResources.GoalNeededPerMonth, MoneyFormatter.Format(needed, CurrencyCode), dateText)
                : string.Format(AppResources.GoalTargetDatePassed, dateText);
        }

        private string PaceFor()
        {
            if (MonthlyPace is not { } pace)
                return AppResources.GoalNotEnoughHistory;
            if (pace <= 0)
                return string.Format(AppResources.GoalNoRecentSavings, GoalProjection.PaceMonths);
            return ProjectedDate is { } projected
                ? string.Format(AppResources.GoalAtYourPace, DateFormatter.Format(projected, DateStyle.MonthYear))
                : AppResources.GoalNotInSight;
        }
    }
}
