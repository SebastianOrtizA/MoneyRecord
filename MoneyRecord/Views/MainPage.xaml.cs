using MoneyRecord.Controls;
using MoneyRecord.Helpers;
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
            ApplyFontScale();
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
            ApplyFontScale();
            // The load shows its own spinner and errors; the page renders right away
            await _viewModel.InitializeAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _viewModel.Deactivate();
        }

        public bool TryCloseOverlay() => AddEntry.TryClose();

        /// <summary>
        /// Income, Expenses and Net side by side, or one under the other when the system font is large: an amount like
        /// "-$1,405.50" doesn't fit a third of the width at 2x and must never be cut.
        /// </summary>
        private void ApplyFontScale()
        {
            var tiles = PeriodTiles.Children.OfType<View>().ToList();
            var stacked = FontScaleLayout.StacksPeriodTiles(ScaledLayout.FontScale);
            PeriodTiles.ColumnDefinitions = stacked
                ? [new ColumnDefinition(GridLength.Star)]
                : [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)];
            PeriodTiles.RowDefinitions = stacked
                ? [.. tiles.Select(_ => new RowDefinition(GridLength.Auto))]
                : [];
            PeriodTiles.RowSpacing = stacked ? 8 : 0;
            for (var i = 0; i < tiles.Count; i++)
            {
                Grid.SetRow(tiles[i], stacked ? i : 0);
                Grid.SetColumn(tiles[i], stacked ? 0 : i);
            }
        }

        /// <summary>Puts the cards in the saved order; the "Customize" link stays last.</summary>
        private void ArrangeCards()
        {
            var cards = new Dictionary<HomeCard, View>
            {
                [HomeCard.Balance] = BalanceCard,
                [HomeCard.PeriodSummary] = PeriodSummaryCard,
                [HomeCard.GetStarted] = StartChecklistCard,
                [HomeCard.Recent] = RecentCard,
                [HomeCard.Upcoming] = UpcomingCard,
                [HomeCard.Budgets] = BudgetsCard,
                [HomeCard.Trip] = ActiveTripCard,
                [HomeCard.Goals] = GoalsCard,
                [HomeCard.Debts] = DebtsCard
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
