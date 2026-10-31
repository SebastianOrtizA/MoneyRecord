using System.Windows.Input;
using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Controls
{
    /// <summary>
    /// One filter as a row: its name, what it is set to (<see cref="Value"/> while <see cref="IsActive"/>, else "All"), and a
    /// chevron. A tap runs <see cref="Command"/> (a pick list).
    /// </summary>
    public class FilterRow : Grid
    {
        public static readonly BindableProperty TitleProperty =
            BindableProperty.Create(nameof(Title), typeof(string), typeof(FilterRow), string.Empty,
                propertyChanged: (bindable, _, _) => ((FilterRow)bindable).Update());

        public static readonly BindableProperty ValueProperty =
            BindableProperty.Create(nameof(Value), typeof(string), typeof(FilterRow), string.Empty,
                propertyChanged: (bindable, _, _) => ((FilterRow)bindable).Update());

        public static readonly BindableProperty IsActiveProperty =
            BindableProperty.Create(nameof(IsActive), typeof(bool), typeof(FilterRow), false,
                propertyChanged: (bindable, _, _) => ((FilterRow)bindable).Update());

        public static readonly BindableProperty CommandProperty =
            BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(FilterRow));

        private static readonly Color PrimaryLight = Color.FromArgb("#512BD4");
        private static readonly Color PrimaryDark = Color.FromArgb("#AC99EA");

        private readonly Label _title = new() { FontSize = 16, VerticalOptions = LayoutOptions.Center };
        private readonly Label _value = new() { FontSize = 16, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation };

        public FilterRow()
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ];
            ColumnSpacing = 8;
            MinimumHeightRequest = 48;

            SetColumn((BindableObject)_value, 1);
            _value.HorizontalOptions = LayoutOptions.End;
            _value.HorizontalTextAlignment = TextAlignment.End;
            var chevron = new Label
            {
                Text = "\U000F0142",
                FontFamily = "MaterialDesignIcons",
                FontSize = 22,
                VerticalOptions = LayoutOptions.Center
            };
            SetColumn((BindableObject)chevron, 2);
            Children.Add(_title);
            Children.Add(_value);
            Children.Add(chevron);

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                if (Command?.CanExecute(null) == true)
                    Command.Execute(null);
            };
            GestureRecognizers.Add(tap);
            Update();
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        /// <summary>What the filter is set to; shown only while <see cref="IsActive"/>.</summary>
        public string Value
        {
            get => (string)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public bool IsActive
        {
            get => (bool)GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }

        public ICommand? Command
        {
            get => (ICommand?)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        private void Update()
        {
            _title.Text = Title;
            _value.Text = IsActive ? Value : AppResources.All;
            _value.FontAttributes = IsActive ? FontAttributes.Bold : FontAttributes.None;
            if (IsActive)
                _value.SetAppThemeColor(Label.TextColorProperty, PrimaryLight, PrimaryDark);
            else
                _value.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#666666"), Color.FromArgb("#AAAAAA"));
        }
    }
}
