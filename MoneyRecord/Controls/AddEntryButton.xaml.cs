using MoneyRecord.ViewModels;
using System.ComponentModel;

namespace MoneyRecord.Controls
{
    public partial class AddEntryButton : ContentView
    {
        private readonly AddEntryViewModel _viewModel;

        public AddEntryButton()
        {
            InitializeComponent();
            _viewModel = IPlatformApplication.Current!.Services.GetRequiredService<AddEntryViewModel>();
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            BindingContext = _viewModel;
        }

        /// <summary>True when the sheet was open and is now closed: lets a page's back button close it first.</summary>
        public bool TryClose()
        {
            if (!_viewModel.IsOpen)
                return false;

            _viewModel.IsOpen = false;
            return true;
        }

        private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(AddEntryViewModel.IsOpen) || !_viewModel.IsOpen)
                return;

            await Sheet.SlideUpAsync(400);
        }
    }
}
