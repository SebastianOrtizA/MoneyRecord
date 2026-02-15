using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class BudgetPage : ContentPage
    {
        private readonly BudgetViewModel _viewModel;

        public BudgetPage(BudgetViewModel viewModel)
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
