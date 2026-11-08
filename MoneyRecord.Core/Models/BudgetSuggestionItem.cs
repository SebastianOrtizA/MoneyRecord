using CommunityToolkit.Mvvm.ComponentModel;
using MoneyRecord.Helpers;

namespace MoneyRecord.Models
{
    /// <summary>A suggested budget in the suggestions sheet: ticked ones are created.</summary>
    public partial class BudgetSuggestionItem(BudgetSuggestion suggestion) : ObservableObject
    {
        public BudgetSuggestion Suggestion { get; } = suggestion;

        public string Name => Suggestion.Name;

        public decimal Amount => Suggestion.Amount;

        public string DisplayIcon => IconHelper.GetCategoryDisplayIcon(Suggestion.IconCode);

        [ObservableProperty]
        private bool isSelected = true;
    }
}
