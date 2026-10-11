using MoneyRecord.Helpers;
using SQLite;

namespace MoneyRecord.Models
{
    public class Category
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public CategoryType Type { get; set; }

        /// <summary>
        /// Unicode code for Material Design Icon (e.g., "F09B5" for food icon)
        /// </summary>
        public string IconCode { get; set; } = "F0770"; // Default: tag icon

        /// <summary>
        /// Gets the displayable icon character from the unicode code
        /// </summary>
        [Ignore]
        public string DisplayIcon => IconHelper.GetCategoryDisplayIcon(IconCode);
    }

    public enum CategoryType
    {
        Income,
        Expense
    }

    /// <summary>
    /// Represents an icon option for category selection
    /// </summary>
    public class CategoryIcon : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string DisplayIcon => IconHelper.GetCategoryDisplayIcon(Code);
    }
}
