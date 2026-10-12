using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class MorePage : ContentPage
    {
        public MorePage(MoreViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
