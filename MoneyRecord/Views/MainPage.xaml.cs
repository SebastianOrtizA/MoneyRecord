using MoneyRecord.Controls;
using MoneyRecord.Models;
using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class MainPage : ContentPage, IOverlayHost
    {
        private readonly MainViewModel _viewModel;

        public MainPage(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
            _viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.Layout))
                    ArrangeCards();
            };
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            // The load shows its own spinner and errors; the page renders right away
            await _viewModel.InitializeAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _viewModel.Deactivate();
        }

        public bool TryCloseOverlay() => AddEntry.TryClose();

        /// <summary>Puts the cards in the saved order; the "Customize" link stays last.</summary>
        private void ArrangeCards()
        {
            var cards = new Dictionary<HomeCard, View>
            {
                [HomeCard.Balance] = BalanceCard,
                [HomeCard.PeriodSummary] = PeriodSummaryCard,
                [HomeCard.Recent] = RecentCard,
                [HomeCard.Upcoming] = UpcomingCard,
                [HomeCard.Budgets] = BudgetsCard,
                [HomeCard.Trip] = ActiveTripCard,
                [HomeCard.Goals] = GoalsCard
            };

            for (var i = 0; i < _viewModel.Layout.Order.Count; i++)
            {
                var view = cards[_viewModel.Layout.Order[i]];
                if (CardStack.Children.IndexOf(view) == i)
                    continue;

                CardStack.Remove(view);
                CardStack.Insert(i, view);
            }
        }
    }
}
