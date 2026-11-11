using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Views;

namespace MoneyRecord.Controls
{
    /// <summary>
    /// Picks a currency from <see cref="ItemsSource"/> (the whole ISO list is too long for a Picker): a tap opens
    /// <see cref="CurrencyPickerPage"/> with a search box. Bind <see cref="SelectedItem"/> like a Picker's.
    /// </summary>
    public partial class CurrencyPicker : Border
    {
        public static readonly BindableProperty ItemsSourceProperty =
            BindableProperty.Create(nameof(ItemsSource), typeof(IReadOnlyList<Currency>), typeof(CurrencyPicker));

        public static readonly BindableProperty SelectedItemProperty =
            BindableProperty.Create(nameof(SelectedItem), typeof(Currency), typeof(CurrencyPicker), null, BindingMode.TwoWay,
                propertyChanged: (bindable, _, _) => ((CurrencyPicker)bindable).ShowSelection());

        public CurrencyPicker()
        {
            InitializeComponent();
            SemanticProperties.SetDescription(this, AppResources.SelectCurrency);
            ShowSelection();
        }

        public IReadOnlyList<Currency>? ItemsSource
        {
            get => (IReadOnlyList<Currency>?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public Currency? SelectedItem
        {
            get => (Currency?)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        private void ShowSelection() => ValueLabel.Text = SelectedItem?.ToString() ?? AppResources.SelectCurrency;

        private bool _isOpen;

        private async void OnTapped(object? sender, TappedEventArgs e)
        {
            // A quick double tap must not stack two lists
            if (ItemsSource == null || _isOpen)
                return;

            _isOpen = true;
            try
            {
                var page = new CurrencyPickerPage(ItemsSource);
                await Navigation.PushModalAsync(page, animated: false);
                var picked = await page.Result;
                if (picked != null)
                    SelectedItem = picked;
            }
            finally
            {
                _isOpen = false;
            }
        }
    }
}
