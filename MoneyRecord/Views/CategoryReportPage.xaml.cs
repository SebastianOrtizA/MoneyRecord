using MoneyRecord.Models;
using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    /// <summary>The Expense or Income report; the two flyout routes are the subclasses below.</summary>
    public partial class CategoryReportPage : ContentPage
    {
        private readonly CategoryReportViewModel _viewModel;

        protected CategoryReportPage(CategoryReportViewModel viewModel, CategoryType type)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _viewModel.Type = type;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.InitializeAsync();
        }
    }

    public sealed class ExpenseReportPage(CategoryReportViewModel viewModel)
        : CategoryReportPage(viewModel, CategoryType.Expense);

    public sealed class IncomeReportPage(CategoryReportViewModel viewModel)
        : CategoryReportPage(viewModel, CategoryType.Income);
}
