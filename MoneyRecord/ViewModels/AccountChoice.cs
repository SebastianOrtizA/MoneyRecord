using CommunityToolkit.Mvvm.ComponentModel;
using MoneyRecord.Models;

namespace MoneyRecord.ViewModels
{
    /// <summary>A chip of the add form's account row; <see cref="IsSelected"/> highlights the chosen account.</summary>
    public partial class AccountChoice : ObservableObject
    {
        [ObservableProperty]
        private bool isSelected;

        public AccountChoice(Account account)
        {
            Account = account;
        }

        public Account Account { get; }
    }
}
