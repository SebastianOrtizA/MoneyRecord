using System.Windows.Input;
using MoneyRecord.Services;

namespace MoneyRecord.Controls
{
    /// <summary>
    /// One row of a pick-one list in a sheet (the period list, a challenge's views): its text, and a check in the
    /// primary colour while <see cref="IsSelected"/>. A tap runs <see cref="Command"/>.
    /// </summary>
    public class OptionRow : Grid
    {
        public static readonly BindableProperty TextProperty =
            BindableProperty.Create(nameof(Text), typeof(string), typeof(OptionRow), string.Empty,
                propertyChanged: (bindable, _, _) => ((OptionRow)bindable).Update());

        public static readonly BindableProperty IsSelectedProperty =
            BindableProperty.Create(nameof(IsSelected), typeof(bool), typeof(OptionRow), false,
                propertyChanged: (bindable, _, _) => ((OptionRow)bindable).Update());

        public static readonly BindableProperty CommandProperty =
            BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(OptionRow));

        public static readonly BindableProperty CommandParameterProperty =
            BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(OptionRow));

        private readonly Label _text = new() { FontSize = 16, VerticalOptions = LayoutOptions.Center };
        private readonly Label _check = new()
        {
            Text = "\U000F012C",
            FontFamily = "MaterialDesignIcons",
            FontSize = 22,
            VerticalOptions = LayoutOptions.Center
        };

        public OptionRow()
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)];
            MinimumHeightRequest = 48;
            SetColumn((BindableObject)_check, 1);
            ThemeColors.Apply(_check, "Primary", "PrimaryDark");
            Children.Add(_text);
            Children.Add(_check);

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                if (Command?.CanExecute(CommandParameter) == true)
                    Command.Execute(CommandParameter);
            };
            GestureRecognizers.Add(tap);
            Update();
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        public ICommand? Command
        {
            get => (ICommand?)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public object? CommandParameter
        {
            get => GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }

        private void Update()
        {
            _text.Text = Text;
            _check.IsVisible = IsSelected;
            // TalkBack reads the text, and whether it is the current choice
            SemanticProperties.SetDescription(this, IsSelected ? $"{Text}, {LocalizationService.Instance.GetString("SelectedState")}" : Text);
            _text.FontAttributes = IsSelected ? FontAttributes.Bold : FontAttributes.None;
            if (IsSelected)
                ThemeColors.Apply(_text, "Primary", "PrimaryDark");
            else
                ThemeColors.Apply(_text, "Black", "White");
        }
    }
}
