namespace MoneyRecord.Controls
{
    /// <summary>
    /// What a list or report shows when it has nothing yet: <see cref="Icon"/> (a Material Design Icons character),
    /// <see cref="Title"/> and <see cref="Message"/>; an empty title or message is left out.
    /// </summary>
    public partial class EmptyState : ContentView
    {
        public static readonly BindableProperty IconProperty = BindableProperty.Create(nameof(Icon), typeof(string), typeof(EmptyState),
            string.Empty, propertyChanged: (b, _, _) => ((EmptyState)b).Refresh());

        public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title), typeof(string), typeof(EmptyState),
            string.Empty, propertyChanged: (b, _, _) => ((EmptyState)b).Refresh());

        public static readonly BindableProperty MessageProperty = BindableProperty.Create(nameof(Message), typeof(string), typeof(EmptyState),
            string.Empty, propertyChanged: (b, _, _) => ((EmptyState)b).Refresh());

        public EmptyState()
        {
            InitializeComponent();
        }

        public string Icon
        {
            get => (string)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        private void Refresh()
        {
            IconLabel.Text = Icon;
            IconLabel.IsVisible = !string.IsNullOrEmpty(Icon);
            TitleLabel.Text = Title;
            TitleLabel.IsVisible = !string.IsNullOrEmpty(Title);
            MessageLabel.Text = Message;
            MessageLabel.IsVisible = !string.IsNullOrEmpty(Message);
        }
    }
}
