using CommunityToolkit.Mvvm.ComponentModel;
using MoneyRecord.Helpers;
using SQLite;

namespace MoneyRecord.Models
{
    /// <summary>
    /// Represents a budget limit for a specific expense category
    /// </summary>
    public class Budget
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int CategoryId { get; set; }

        public decimal LimitAmount { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true;
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

        [ObservableProperty]
        private decimal limitAmount;

        [ObservableProperty]
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
        private Color progressColor = Colors.Green;

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

            // Set progress bar color based on percentage
            ProgressColor = ProgressPercentage switch
            {
                >= 100 => Colors.Red,
                >= 80 => Colors.Orange,
                >= 60 => Colors.Yellow,
                _ => Colors.Green
            };
        }
    }
}
