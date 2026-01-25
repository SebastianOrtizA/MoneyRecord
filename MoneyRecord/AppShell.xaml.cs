using MoneyRecord.Views;

namespace MoneyRecord
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(AddTransactionPage), typeof(AddTransactionPage));
            Routing.RegisterRoute(nameof(ManageCategoriesPage), typeof(ManageCategoriesPage));
            Routing.RegisterRoute(nameof(ManageAccountsPage), typeof(ManageAccountsPage));
        }
    }
}
