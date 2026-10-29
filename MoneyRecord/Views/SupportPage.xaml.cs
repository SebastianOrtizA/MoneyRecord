using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class SupportPage : ContentPage
    {
        private readonly SupportViewModel _viewModel;

        public SupportPage(SupportViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _viewModel.LoadCommand.Execute(null);
        }
    }
}
