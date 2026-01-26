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

        public string DisplayIcon
        {
            get
            {
                try
                {
                    if (string.IsNullOrEmpty(Code))
                        return "\uF0770";

                    var codePoint = Convert.ToInt32(Code, 16);
                    return char.ConvertFromUtf32(codePoint);
                }
                catch
                {
                    return "\uF0770";
                }
            }
        }
    }
}
