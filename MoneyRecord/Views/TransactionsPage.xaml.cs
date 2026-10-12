using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class TransactionsPage : ContentPage
    {
        private readonly TransactionsViewModel _viewModel;

        public TransactionsPage(TransactionsViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            // The load shows its own spinner and errors; the page renders right away
            await _viewModel.InitializeAsync();
        }
    }
}
