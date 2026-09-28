using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class RecurringTransactionsPage : ContentPage
    {
        private readonly RecurringTransactionsViewModel _viewModel;

        public RecurringTransactionsPage(RecurringTransactionsViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.InitializeAsync();
        }
    }
}
