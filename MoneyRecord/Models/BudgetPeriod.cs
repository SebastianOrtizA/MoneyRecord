namespace MoneyRecord.Models
{
    /// <summary>
    /// Represents the period type for a budget limit
    /// </summary>
    public enum BudgetPeriod
    {
        /// <summary>
        /// Budget limit is set per day
        /// </summary>
        Day,
        
        /// <summary>
        /// Budget limit is set per month
        /// </summary>
        Month,
        
        /// <summary>
        /// Budget limit is set per year
        /// </summary>
        Year
    }
}
