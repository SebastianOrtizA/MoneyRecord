using CommunityToolkit.Mvvm.ComponentModel;
using MoneyRecord.Helpers;
using MoneyRecord.Models;

namespace MoneyRecord.ViewModels
{
    /// <summary>
    /// An account or a pocket in the goal sheet's list (<see cref="Option"/>); <see cref="IsSelected"/> links it to the goal.
    /// </summary>
    public partial class GoalAccountChoice : ObservableObject
    {
        [ObservableProperty]
        private bool isSelected;

        public GoalAccountChoice(GoalLinkOption option, bool isSelected)
        {
            Option = option;
            this.isSelected = isSelected;
        }

        public GoalLinkOption Option { get; }

        public bool IsPocket => Option.IsPocket;

        /// <summary>The account ID or the pocket ID, as the goal links it.</summary>
        public int Id => Option.Id;

        /// <summary>"Bank" or "Bank › Travel".</summary>
        public string Name => Option.Name;

        public string DisplayIcon => Option.Pocket?.DisplayIcon ?? Option.Account.DisplayIcon;

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
