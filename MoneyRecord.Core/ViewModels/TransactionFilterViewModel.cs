using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;

namespace MoneyRecord.ViewModels
{
    /// <summary>
    /// Home's search box and filter panel. <see cref="Changed"/> fires when the filter changes: typed text once the
    /// typing stops, pickers and buttons at once.
    /// </summary>
    public partial class TransactionFilterViewModel : ObservableObject
    {
        public static readonly TimeSpan DefaultTypingDelay = TimeSpan.FromMilliseconds(300);

        private readonly Debouncer _typingDebouncer;
        private bool _clearing;

        [ObservableProperty]
        private bool isVisible;

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private TypeFilterOption? selectedType;

        [ObservableProperty]
        private FilterOption? selectedCategory;

        [ObservableProperty]
        private FilterOption? selectedAccount;

        [ObservableProperty]
        private string minAmountText = string.Empty;

        [ObservableProperty]
        private string maxAmountText = string.Empty;

        [ObservableProperty]
        private List<FilterOption> categories = [];

        [ObservableProperty]
        private List<FilterOption> accounts = [];

        public List<TypeFilterOption> Types { get; } =
        [
            new(null, AppResources.All),
            new(TransactionType.Income, AppResources.Incomes),
            new(TransactionType.Expense, AppResources.Expenses),
            new(TransactionType.Transfer, AppResources.Transfers)
        ];

        public TransactionFilterViewModel(TimeSpan? typingDelay = null, TimeProvider? timeProvider = null)
        {
            _typingDebouncer = new Debouncer(typingDelay ?? DefaultTypingDelay, timeProvider);
            selectedType = Types[0];
        }

        public event Action? Changed;

        public TransactionFilter ToFilter(bool ascending) => new(
            SearchText,
            SelectedType?.Type,
            SelectedCategory?.Id,
            SelectedAccount?.Id,
            ParseAmountLimit(MinAmountText),
            ParseAmountLimit(MaxAmountText),
            ascending);

        /// <summary>The category and account pickers offer what the loaded entries use.</summary>
        public void SetOptions(IReadOnlyCollection<LedgerEntry> entries)
        {
            Categories = LedgerFilter.CategoryOptions(entries);
            Accounts = LedgerFilter.AccountOptions(entries);
        }

        /// <summary>Empty = no limit; otherwise read like amount entry (base currency).</summary>
        private static decimal? ParseAmountLimit(string text) =>
            string.IsNullOrWhiteSpace(text) ? null : AmountParser.Parse(text, null);

        [RelayCommand]
        private void Toggle() => IsVisible = !IsVisible;

        [RelayCommand]
        private void Clear()
        {
            _clearing = true;
            try
            {
                SearchText = string.Empty;
                SelectedType = Types[0];
                SelectedCategory = null;
                SelectedAccount = null;
                MinAmountText = string.Empty;
                MaxAmountText = string.Empty;
            }
            finally
            {
                _clearing = false;
            }

            RaiseChanged();
        }

        /// <summary>The keyboard's search key: no need to wait for the pause.</summary>
        [RelayCommand]
        private void Search() => RaiseChanged();

        partial void OnSearchTextChanged(string value) => OnTyped();

        partial void OnMinAmountTextChanged(string value) => OnTyped();

        partial void OnMaxAmountTextChanged(string value) => OnTyped();

        partial void OnSelectedTypeChanged(TypeFilterOption? value) => RaiseChanged();

        partial void OnSelectedCategoryChanged(FilterOption? value) => RaiseChanged();

        partial void OnSelectedAccountChanged(FilterOption? value) => RaiseChanged();

        private void OnTyped()
        {
            if (!_clearing)
                _ = _typingDebouncer.RunAsync(RaiseChanged);
        }

        private void RaiseChanged()
        {
            if (_clearing)
                return;

            _typingDebouncer.Cancel();
            Changed?.Invoke();
        }
    }
}
