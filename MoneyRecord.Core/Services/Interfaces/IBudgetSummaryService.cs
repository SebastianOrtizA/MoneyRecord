using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>Budget progress for a period, shared by the Budget page and the Home budget card.</summary>
    public interface IBudgetSummaryService
    {
        /// <summary>
        /// Each active budget's spending in the period, its limit projected onto the period
        /// (see <see cref="Helpers.BudgetProjectionHelper"/>), highest progress first.
        /// </summary>
        Task<List<BudgetProgress>> GetSummaryAsync(PeriodType period, DateTime customStartDate = default, DateTime customEndDate = default);

        /// <summary>Monthly limits for the expense categories without a budget, from the last <see cref="Helpers.BudgetSuggester.Months"/> full months; only categories that spent something.</summary>
        Task<List<Helpers.BudgetSuggestion>> GetSuggestionsAsync(DateTime today);
    }
}
