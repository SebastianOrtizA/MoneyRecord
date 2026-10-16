using CommunityToolkit.Mvvm.ComponentModel;
using MoneyRecord.Models;

namespace MoneyRecord.ViewModels
{
    /// <summary>One row of the form's split: a category and its share of the amount (as typed).</summary>
    public partial class SplitPartViewModel : ObservableObject
    {
        /// <param name="categories">The categories it can take (the form's), so the picker has its items before its selection.</param>
        public SplitPartViewModel(IReadOnlyList<Category> categories)
        {
            Categories = categories;
        }

        public IReadOnlyList<Category> Categories { get; }

        [ObservableProperty]
        private Category? category;

        [ObservableProperty]
        private string amount = string.Empty;

        /// <summary>The account's currency, for the amount field's format.</summary>
        [ObservableProperty]
        private string currencyCode = string.Empty;

        /// <summary>The category or amount changed: the form updates what is left to assign.</summary>
        public event EventHandler? Changed;

        partial void OnCategoryChanged(Category? value) => Changed?.Invoke(this, EventArgs.Empty);

        partial void OnAmountChanged(string value) => Changed?.Invoke(this, EventArgs.Empty);
    }
}
