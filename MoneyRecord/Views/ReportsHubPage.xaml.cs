using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class ReportsHubPage : ContentPage
    {
        public ReportsHubPage(ReportsHubViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
