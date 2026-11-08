using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using MoneyRecord.Charts;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>The Forecast page's view model: the shared one plus the chart, drawn when a load finishes (so it follows the theme).</summary>
    public partial class ForecastChartViewModel(IForecastService forecast, PeriodViewModel period, IConversionService conversion,
        IErrorHandler errorHandler, INavigationService navigationService)
        : ForecastViewModel(forecast, period, conversion, errorHandler, navigationService)
    {
        [ObservableProperty]
        private ISeries[] forecastSeries = [];

        [ObservableProperty]
        private Axis[] dateAxis = [];

        [ObservableProperty]
        private Axis[] moneyAxis = [];

        [ObservableProperty]
        private RectangularSection[] forecastSections = [];

        protected override void OnViewBuilt(ForecastView newView)
        {
            DateAxis = ChartFactory.DateAxis(newView.Dates);
            MoneyAxis = ChartFactory.MoneyAxis(newView.CurrencyCode, fromZero: false);
            ForecastSections = ChartFactory.ForecastSections(newView);
            ForecastSeries = ChartFactory.Forecast(newView);
        }
    }
}
