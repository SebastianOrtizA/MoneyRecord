using System.Windows.Input;
using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Controls
{
    /// <summary>
    /// The add forms' Expense | Income | Transfer switch: three chips, the <see cref="Selected"/> one active. A tap runs
    /// <see cref="Command"/> with "Expense", "Income" or "Transfer".
    /// </summary>
    public class EntryTypeSwitch : FlexLayout
    {
        public static readonly BindableProperty SelectedProperty =
            BindableProperty.Create(nameof(Selected), typeof(string), typeof(EntryTypeSwitch), string.Empty,
                propertyChanged: (b, _, _) => ((EntryTypeSwitch)b).Refresh());

        public static readonly BindableProperty CommandProperty =
            BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(EntryTypeSwitch));

        private readonly (string Kind, FilterChip Chip)[] _chips;

        public EntryTypeSwitch()
        {
            // Wraps to a second row when a large font leaves no room for three chips
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap;
            _chips =
            [
                Make("Expense", AppResources.Expense),
                Make("Income", AppResources.Income),
                Make("Transfer", AppResources.Transfer)
            ];
            Refresh();
        }

        public string Selected
        {
            get => (string)GetValue(SelectedProperty);
            set => SetValue(SelectedProperty, value);
        }

        public ICommand? Command
        {
            get => (ICommand?)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        private (string, FilterChip) Make(string kind, string text)
        {
            var chip = new FilterChip { Text = text, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(16, 0) };
            // Equal thirds while they fit; a large font wraps them instead
            FlexLayout.SetGrow(chip, 1);
            FlexLayout.SetBasis(chip, new Microsoft.Maui.Layouts.FlexBasis(0.3f, true));
            chip.Clicked += (_, _) =>
            {
                if (Command?.CanExecute(kind) == true)
                    Command.Execute(kind);
            };
            Children.Add(chip);
            return (kind, chip);
        }

        private void Refresh()
        {
            foreach (var (kind, chip) in _chips)
                chip.IsActive = kind == Selected;
        }
    }
}
