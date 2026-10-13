using MoneyRecord.Models;
using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    /// <summary>The Spending or Income report; the two Reports top tabs are the subclasses below.</summary>
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

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _viewModel.Deactivate();
        }
    }

    public sealed class ExpenseReportPage(CategoryReportViewModel viewModel)
        : CategoryReportPage(viewModel, CategoryType.Expense);

    public sealed class IncomeReportPage(CategoryReportViewModel viewModel)
        : CategoryReportPage(viewModel, CategoryType.Income);
}
