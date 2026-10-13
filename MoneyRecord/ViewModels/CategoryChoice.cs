using CommunityToolkit.Mvvm.ComponentModel;
using MoneyRecord.Models;

namespace MoneyRecord.ViewModels
{
    /// <summary>A cell of the add form's category grid; <see cref="IsSelected"/> highlights the chosen one.</summary>
    public partial class CategoryChoice : ObservableObject
    {
        [ObservableProperty]
        private bool isSelected;

        public CategoryChoice(Category category)
        {
            Category = category;
        }

        public Category Category { get; }
    }
}
