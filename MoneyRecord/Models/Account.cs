using SQLite;

namespace MoneyRecord.Models
{
    public class Account
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public decimal InitialBalance { get; set; }

        public bool IsDefault { get; set; }

        /// <summary>
        /// Unicode code for Material Design Icon (e.g., "F0070" for bank icon)
        /// </summary>
        public string IconCode { get; set; } = "F0070"; // Default: bank icon

        /// <summary>
        /// The date when the account was created or when the initial balance was assigned.
        /// Used as fallback for last activity date when no transactions or transfers exist.
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.Now;

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
                        return "\uF0070";

                    var codePoint = Convert.ToInt32(IconCode, 16);
                    return char.ConvertFromUtf32(codePoint);
                }
                catch
                {
                    return "\uF0070";
                }
            }
        }
    }

    /// <summary>
    /// Represents an icon option for account selection
    /// </summary>
    public class AccountIcon : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
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
                        return "\uF0070";

                    var codePoint = Convert.ToInt32(Code, 16);
                    return char.ConvertFromUtf32(codePoint);
                }
                catch
                {
                    return "\uF0070";
                }
            }
        }
    }
}
