using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class ImportStatementPage : ContentPage
    {
        private readonly ImportStatementViewModel _viewModel;

        public ImportStatementPage(ImportStatementViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _viewModel = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.InitializeAsync();
        }
    }
}
