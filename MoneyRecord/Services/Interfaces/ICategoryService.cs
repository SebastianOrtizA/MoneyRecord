namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// What still points at a category.
    /// </summary>
    public sealed record CategoryUsage(int Transactions, int RecurringRules, int Budgets)
    {
        /// <summary>
        /// Transactions and recurring rules can't be left without a category, so they need a replacement.
        /// </summary>
        public bool NeedsReplacement => Transactions > 0 || RecurringRules > 0;
    }

    public sealed record CategoryDeleteResult(int TransactionsMoved, int RecurringRulesMoved, int BudgetsMoved, int BudgetsDeleted);

    /// <summary>
    /// Category operations that span several tables.
    /// </summary>
    public interface ICategoryService
    {
        Task<CategoryUsage> GetUsageAsync(int categoryId);

        /// <summary>
        /// In one database transaction: moves transactions and recurring rules to the replacement category, moves the
        /// budgets there too when it has none (otherwise deletes them), then deletes the category.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// A replacement is needed but missing, or it is the same category or of another type.
        /// </exception>
        Task<CategoryDeleteResult> DeleteAsync(int categoryId, int? replacementId);
    }
}
