using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using System.Collections.ObjectModel;

namespace MoneyRecord.ViewModels
{
    /// <summary>One row of the Home screen editor: a card, its switch and where it sits.</summary>
    public partial class HomeCardItem(HomeCard card, string title, bool isShown, Action changed) : ObservableObject
    {
        public HomeCard Card { get; } = card;

        public string Title { get; } = title;

        /// <summary>Balance, the app's main figure, always shows.</summary>
        public bool CanHide => Card != HomeCard.Balance;

        [ObservableProperty]
        private bool isShown = isShown;

        [ObservableProperty]
        private bool canMoveUp;

        [ObservableProperty]
        private bool canMoveDown;

        partial void OnIsShownChanged(bool value) => changed();
    }

    /// <summary>Settings → Home screen: reorder the Home cards and switch them on or off (saved at every change).</summary>
    public partial class HomeLayoutViewModel : ObservableObject
    {
        private readonly IHomeLayoutSettings _settings;
        private bool _loading;

        public ObservableCollection<HomeCardItem> Items { get; } = [];

        public HomeLayoutViewModel(IHomeLayoutSettings settings)
        {
            _settings = settings;
            Load(settings.Layout);
        }

        public void Initialize() => Load(_settings.Layout);

        private void Load(HomeLayout layout)
        {
            _loading = true;
            Items.Clear();
            foreach (var card in layout.Order)
                Items.Add(new HomeCardItem(card, TitleOf(card), layout.IsShown(card), Save));
            _loading = false;
            RefreshMoves();
        }

        private void Save()
        {
            if (_loading)
                return;

            _settings.Layout = new HomeLayout(Items.Select(i => i.Card).ToList(), Items.Where(i => !i.IsShown).Select(i => i.Card).ToHashSet());
        }

        private void RefreshMoves()
        {
            for (var i = 0; i < Items.Count; i++)
            {
                Items[i].CanMoveUp = i > 0;
                Items[i].CanMoveDown = i < Items.Count - 1;
            }
        }

        private void Move(HomeCardItem? item, int offset)
        {
            var index = item == null ? -1 : Items.IndexOf(item);
            var target = index + offset;
            if (index < 0 || target < 0 || target >= Items.Count)
                return;

            Items.Move(index, target);
            RefreshMoves();
            Save();
        }

        /// <summary>A card dropped on another takes its place.</summary>
        public void MoveTo(HomeCardItem item, HomeCardItem target)
        {
            var to = Items.IndexOf(target);
            var from = Items.IndexOf(item);
            if (from < 0 || to < 0 || from == to)
                return;

            Items.Move(from, to);
            RefreshMoves();
            Save();
        }

        [RelayCommand]
        private void MoveUp(HomeCardItem? item) => Move(item, -1);

        [RelayCommand]
        private void MoveDown(HomeCardItem? item) => Move(item, 1);

        [RelayCommand]
        private void Reset()
        {
            _settings.Layout = HomeLayout.Default;
            Load(HomeLayout.Default);
        }

        private static string TitleOf(HomeCard card) => card switch
        {
            HomeCard.Balance => AppResources.HomeCardBalance,
            HomeCard.PeriodSummary => AppResources.HomeCardPeriodSummary,
            HomeCard.Recent => AppResources.RecentTransactions,
            HomeCard.Upcoming => AppResources.Upcoming,
            HomeCard.Budgets => AppResources.Budgets,
            HomeCard.Trip => AppResources.HomeCardTrip,
            HomeCard.Goals => AppResources.SavingsGoalsTitle,
            _ => card.ToString()
        };
    }
}
