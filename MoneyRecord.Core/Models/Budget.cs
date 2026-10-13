using CommunityToolkit.Mvvm.ComponentModel;
using MoneyRecord.Helpers;
using MoneyRecord.Resources.Strings;
using SQLite;

namespace MoneyRecord.Models
{
    /// <summary>
    /// Represents a budget limit for a specific expense category
    /// </summary>
    public class Budget : IEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int CategoryId { get; set; }

        /// <summary>
        /// Money value for view models and JSON v1 backups; stored as <see cref="LimitAmountCents"/>.
        /// </summary>
        [Ignore]
        public decimal LimitAmount
        {
            get => Money.FromCents(LimitAmountCents);
            set => LimitAmountCents = Money.ToCents(value);
        }

        /// <summary>
        /// Stored value in cents (see <see cref="Money"/>). Declared after <see cref="LimitAmount"/>, so a JSON backup writes it
        /// later in the file; on import System.Text.Json applies values in file order, so the exact value wins.
        /// </summary>
        public long LimitAmountCents { get; set; }

        /// <summary>
        /// The period type for the budget limit (Day, Month, Year)
        /// </summary>
        public BudgetPeriod Period { get; set; } = BudgetPeriod.Month;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true;
    }

    /// <summary>How much of a budget is used: OnTrack < 60% <= Warning < 80% <= High < 100% <= Exceeded.</summary>
    public enum BudgetLevel
    {
        OnTrack,
        Warning,
        High,
        Exceeded
    }

    /// <summary>
    /// ViewModel-friendly representation of a budget with progress information
    /// </summary>
    public partial class BudgetProgress : ObservableObject
    {
        public int BudgetId { get; set; }

        public int CategoryId { get; set; }

        public string CategoryName { get; set; } = string.Empty;

        public string CategoryIconCode { get; set; } = string.Empty;

        /// <summary>
        /// The period type for the budget (Day, Month, Year)
        /// </summary>
        public BudgetPeriod Period { get; set; } = BudgetPeriod.Month;

        /// <summary>
        /// The original limit amount from the budget definition
        /// </summary>
        [ObservableProperty]
        private decimal originalLimitAmount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SpentOfLimitText))]
        private decimal limitAmount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SpentOfLimitText))]
        private decimal spentAmount;

        [ObservableProperty]
        private double progressPercentage;

        [ObservableProperty]
        private bool isOverBudget;

        [ObservableProperty]
        private decimal exceededAmount;

        [ObservableProperty]
        private decimal remainingAmount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusText), nameof(StatusIcon))]
        private BudgetLevel level = BudgetLevel.OnTrack;

        /// <summary>
        /// "On track" below 60%, "Near limit" from 60%, "Over budget" at the limit: a text label next to the status colour,
        /// so colour isn't the only signal.
        /// </summary>
        public string StatusText => Level switch
        {
            BudgetLevel.Exceeded => AppResources.OverBudget,
            BudgetLevel.Warning or BudgetLevel.High => AppResources.NearLimit,
            _ => AppResources.OnTrack
        };

        /// <summary>Check, alert or alert-circle: the status shape, also for readers who can't tell the colours apart.</summary>
        public string StatusIcon => IconHelper.GetDisplayIcon(Level switch
        {
            BudgetLevel.Exceeded => "F0028",
            BudgetLevel.Warning or BudgetLevel.High => "F0026",
            _ => "F05E0"
        }, "F05E0");

        /// <summary>"$400.00 of $500.00": spent of the limit for the shown period.</summary>
        public string SpentOfLimitText =>
            string.Format(AppResources.BudgetSpentOfLimit, MoneyFormatter.Format(SpentAmount), MoneyFormatter.Format(LimitAmount));

        public string DisplayIcon => IconHelper.GetCategoryDisplayIcon(CategoryIconCode);

        public void CalculateProgress()
        {
            if (LimitAmount > 0)
            {
                ProgressPercentage = Math.Min((double)(SpentAmount / LimitAmount) * 100, 100);
            }
            else
            {
                ProgressPercentage = 0;
            }

            IsOverBudget = SpentAmount > LimitAmount;
            ExceededAmount = IsOverBudget ? SpentAmount - LimitAmount : 0;
            RemainingAmount = IsOverBudget ? 0 : LimitAmount - SpentAmount;

            // The progress bar colour comes from the level (BudgetLevelToColorConverter, light/dark aware)
            Level = ProgressPercentage switch
            {
                >= 100 => BudgetLevel.Exceeded,
                >= 80 => BudgetLevel.High,
                >= 60 => BudgetLevel.Warning,
                _ => BudgetLevel.OnTrack
            };
        }
    }
}
