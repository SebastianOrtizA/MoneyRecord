using MoneyRecord.ViewModels;

namespace MoneyRecord.Controls
{
    public partial class FloatingActionMenu : ContentView
    {
        public FloatingActionMenu()
        {
            InitializeComponent();
            BindingContext = new FloatingMenuViewModel();
        }
    }
}
