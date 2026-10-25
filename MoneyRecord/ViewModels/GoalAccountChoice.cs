using CommunityToolkit.Mvvm.ComponentModel;
using MoneyRecord.Models;

namespace MoneyRecord.ViewModels
{
    /// <summary>
    /// An account or a pocket in the goal sheet's list; <see cref="IsSelected"/> links it to the goal. A pocket is
    /// listed under its account, and the two can't both be ticked (the account's balance already holds the pocket's money).
    /// </summary>
    public partial class GoalAccountChoice : ObservableObject
    {
        [ObservableProperty]
        private bool isSelected;

        public GoalAccountChoice(Account account, bool isSelected)
        {
            Account = account;
            this.isSelected = isSelected;
            Name = account.Name;
            DisplayIcon = account.DisplayIcon;
        }

        public GoalAccountChoice(PocketSummary pocket, bool isSelected)
        {
            Account = pocket.Account;
            Pocket = pocket.Pocket;
            this.isSelected = isSelected;
            Name = pocket.DisplayName;
            DisplayIcon = pocket.DisplayIcon;
        }

        /// <summary>The account, or the pocket's account.</summary>
        public Account Account { get; }

        /// <summary>Set for a pocket choice.</summary>
        public Pocket? Pocket { get; }

        public bool IsPocket => Pocket != null;

        /// <summary>The account ID or the pocket ID, as the goal links it.</summary>
        public int Id => Pocket?.Id ?? Account.Id;

        /// <summary>"Bank" or "Bank › Travel".</summary>
        public string Name { get; }

        public string DisplayIcon { get; }

        /// <summary>A pocket is indented: it sits under its account.</summary>
        public Thickness RowMargin => new(IsPocket ? 28 : 0, 0, 0, 0);

        /// <summary>Raised when this choice is ticked, so the list can untick the choices it can't go with.</summary>
        public event EventHandler? Ticked;

        partial void OnIsSelectedChanged(bool value)
        {
            if (value)
                Ticked?.Invoke(this, EventArgs.Empty);
        }
    }
}
