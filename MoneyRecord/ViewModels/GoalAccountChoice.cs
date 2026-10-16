using CommunityToolkit.Mvvm.ComponentModel;
using MoneyRecord.Models;

namespace MoneyRecord.ViewModels
{
    /// <summary>An account in the goal sheet's list; <see cref="IsSelected"/> links it to the goal.</summary>
    public partial class GoalAccountChoice : ObservableObject
    {
        [ObservableProperty]
        private bool isSelected;

        public GoalAccountChoice(Account account, bool isSelected)
        {
            Account = account;
            this.isSelected = isSelected;
        }

        public Account Account { get; }
    }
}
