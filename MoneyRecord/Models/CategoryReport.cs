namespace MoneyRecord.Models
{
    /// <summary>
    /// Represents expense data for a single category in reports
    /// </summary>
    public class CategoryExpenseData
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string IconCode { get; set; } = "F0770";
        public decimal Amount { get; set; }
        public double Percentage { get; set; }
        public Color HeatmapColor { get; set; } = Colors.Gray;

        /// <summary>
        /// Gets the displayable icon character from the unicode code
        /// </summary>
        public string DisplayIcon
        {
            get
            {
                try
                {
                    if (string.IsNullOrEmpty(IconCode))
                        return "\uF0770";

                    var codePoint = Convert.ToInt32(IconCode, 16);
                    return char.ConvertFromUtf32(codePoint);
                }
                catch
                {
                    return "\uF0770";
                }
            }
        }

        /// <summary>
        /// Indicates if this is part of the "Others" group (categories below 10%)
        /// </summary>
        public bool IsOthersGroup { get; set; }
    }

    /// <summary>
    /// Represents a heatmap cell for display
    /// </summary>
    public class HeatmapCell
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string DisplayIcon { get; set; } = string.Empty;
        public double Percentage { get; set; }
        public decimal Amount { get; set; }
        public Color BackgroundColor { get; set; } = Colors.Gray;
        public Color TextColor { get; set; } = Colors.White;
        public bool IsOthersGroup { get; set; }

        /// <summary>
        /// Display text for the percentage
        /// </summary>
        public string PercentageText => $"{Percentage:F1}%";

        /// <summary>
        /// Size multiplier based on percentage (for visual weight)
        /// </summary>
        public double SizeMultiplier => Math.Max(0.5, Percentage / 100.0 * 2);
    }

    /// <summary>
    /// Represents income data for a single category in reports
    /// </summary>
    public class CategoryIncomeData
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string IconCode { get; set; } = "F0770";
        public decimal Amount { get; set; }
        public double Percentage { get; set; }
        public Color HeatmapColor { get; set; } = Colors.Gray;

        /// <summary>
        /// Gets the displayable icon character from the unicode code
        /// </summary>
        public string DisplayIcon
        {
            get
            {
                try
                {
                    if (string.IsNullOrEmpty(IconCode))
                        return "\uF0770";

                    var codePoint = Convert.ToInt32(IconCode, 16);
                    return char.ConvertFromUtf32(codePoint);
                }
                catch
                {
                    return "\uF0770";
                }
            }
        }

        /// <summary>
        /// Indicates if this is part of the "Others" group (categories below 10%)
        /// </summary>
        public bool IsOthersGroup { get; set; }
    }
}
