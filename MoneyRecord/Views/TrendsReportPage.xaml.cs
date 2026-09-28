using MoneyRecord.Controls;
using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class TrendsReportPage : ContentPage
    {
        private readonly TrendsReportViewModel _viewModel;
        private readonly BarChartDrawable _monthlyDrawable = new();
        private readonly BarChartDrawable _incomeVsExpenseDrawable = new();
        private readonly PieChartDrawable _pieDrawable = new();

        public TrendsReportPage(TrendsReportViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;

            MonthlyChart.Drawable = _monthlyDrawable;
            IncomeVsExpenseChart.Drawable = _incomeVsExpenseDrawable;
            CategoryPieChart.Drawable = _pieDrawable;

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            UpdateDarkMode();
            await _viewModel.InitializeAsync();
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(TrendsReportViewModel.MonthlyChartData):
                    _monthlyDrawable.Data = _viewModel.MonthlyChartData;
                    MonthlyChart.Invalidate();
                    break;
                case nameof(TrendsReportViewModel.IncomeVsExpenseChartData):
                    _incomeVsExpenseDrawable.Data = _viewModel.IncomeVsExpenseChartData;
                    IncomeVsExpenseChart.Invalidate();
                    break;
                case nameof(TrendsReportViewModel.CategoryPieSlices):
                    _pieDrawable.Slices = _viewModel.CategoryPieSlices;
                    CategoryPieChart.Invalidate();
                    break;
            }
        }

        private void UpdateDarkMode()
        {
            bool isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
            _monthlyDrawable.IsDarkMode = isDark;
            _incomeVsExpenseDrawable.IsDarkMode = isDark;
            _pieDrawable.IsDarkMode = isDark;
        }
    }
}
