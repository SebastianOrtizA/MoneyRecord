using System.Windows.Input;
using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Controls
{
    /// <summary>
    /// The add forms' Expense | Income | Transfer switch: three chips, the <see cref="Selected"/> one active. A tap runs
    /// <see cref="Command"/> with "Expense", "Income" or "Transfer".
    /// </summary>
    public class EntryTypeSwitch : Grid
    {
        public static readonly BindableProperty SelectedProperty =
            BindableProperty.Create(nameof(Selected), typeof(string), typeof(EntryTypeSwitch), string.Empty,
                propertyChanged: (b, _, _) => ((EntryTypeSwitch)b).Refresh());

        public static readonly BindableProperty CommandProperty =
            BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(EntryTypeSwitch));

        private readonly (string Kind, FilterChip Chip)[] _chips;

        public EntryTypeSwitch()
        {
            ColumnSpacing = 8;
            ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star), new(GridLength.Star)];
            _chips =
            [
                Make("Expense", AppResources.Expense, 0),
                Make("Income", AppResources.Income, 1),
                Make("Transfer", AppResources.Transfer, 2)
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

        private (string, FilterChip) Make(string kind, string text, int column)
        {
            var chip = new FilterChip { Text = text, Padding = new Thickness(8, 0) };
            chip.Clicked += (_, _) =>
            {
                if (Command?.CanExecute(kind) == true)
                    Command.Execute(kind);
            };
            this.Add(chip, column);
            return (kind, chip);
        }

        private void Refresh()
        {
            foreach (var (kind, chip) in _chips)
                chip.IsActive = kind == Selected;
        }
    }
}
