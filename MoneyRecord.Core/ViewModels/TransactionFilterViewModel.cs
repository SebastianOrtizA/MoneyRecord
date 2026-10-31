using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using System.Globalization;

namespace MoneyRecord.ViewModels
{
    public enum ActiveFilterKind
    {
        Search,
        Type,
        Category,
        Account,
        Tag,
        Amount
    }

    /// <summary>One filter that is on, shown as a chip that removes it.</summary>
    public sealed record ActiveFilterChip(ActiveFilterKind Kind, string Text)
    {
        public string Display => $"{Text}  ✕";
    }

    /// <summary>
    /// The Transactions tab's search box and filters. <see cref="Changed"/> fires when the filter changes: typed text
    /// once the typing stops, picks and buttons at once. A pick list answers through <see cref="Picked"/>; every filter
    /// that is on is a chip in <see cref="ActiveChips"/>.
    /// </summary>
    public partial class TransactionFilterViewModel : ObservableObject
    {
        public static readonly TimeSpan DefaultTypingDelay = TimeSpan.FromMilliseconds(300);

        private readonly IDialogService _dialogService;
        private readonly Debouncer _typingDebouncer;
        private bool _clearing;

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TypeChipText), nameof(IsTypeActive))]
        private TypeFilterOption? selectedType;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CategoryChipText), nameof(IsCategoryActive))]
        private FilterOption? selectedCategory;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AccountChipText), nameof(IsAccountActive))]
        private FilterOption? selectedAccount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TagChipText), nameof(IsTagActive))]
        private string? selectedTag;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AmountChipText), nameof(IsAmountActive))]
        private string minAmountText = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AmountChipText), nameof(IsAmountActive))]
        private string maxAmountText = string.Empty;

        [ObservableProperty]
        private List<FilterOption> categories = [];

        [ObservableProperty]
        private List<FilterOption> accounts = [];

        [ObservableProperty]
        private List<string> tags = [];

        public List<TypeFilterOption> Types { get; } =
        [
            new(null, AppResources.All),
            new(TransactionType.Income, AppResources.Incomes),
            new(TransactionType.Expense, AppResources.Expenses),
            new(TransactionType.Transfer, AppResources.Transfers)
        ];

        /// <param name="dialogService">Shows the chips' pick lists.</param>
        public TransactionFilterViewModel(IDialogService dialogService, TimeSpan? typingDelay = null,
            TimeProvider? timeProvider = null)
        {
            _dialogService = dialogService;
            _typingDebouncer = new Debouncer(typingDelay ?? DefaultTypingDelay, timeProvider);
            selectedType = Types[0];
        }

        public event Action? Changed;

        /// <summary>A pick list answered (type, category, account or tag): the filter is settled, unlike typing an amount.</summary>
        public event Action? Picked;

        public string TypeChipText => IsTypeActive ? SelectedType!.Name : AppResources.FilterByType;

        public bool IsTypeActive => SelectedType?.Type != null;

        public string CategoryChipText => SelectedCategory?.Name ?? AppResources.FilterByCategory;

        public bool IsCategoryActive => SelectedCategory != null;

        public string AccountChipText => SelectedAccount?.Name ?? AppResources.FilterByAccount;

        public bool IsAccountActive => SelectedAccount != null;

        public string TagChipText => SelectedTag ?? AppResources.FilterByTag;

        public bool IsTagActive => SelectedTag != null;

        /// <summary>"10 – 50", "≥ 10" or "≤ 50" (plain numbers: the limits apply to every currency), else "Amount".</summary>
        public string AmountChipText => (ParseAmountLimit(MinAmountText), ParseAmountLimit(MaxAmountText)) switch
        {
            (decimal min, decimal max) => $"{FormatLimit(min)} – {FormatLimit(max)}",
            (decimal min, null) => $"≥ {FormatLimit(min)}",
            (null, decimal max) => $"≤ {FormatLimit(max)}",
            _ => AppResources.FilterByAmount
        };

        public bool IsAmountActive => ParseAmountLimit(MinAmountText) != null || ParseAmountLimit(MaxAmountText) != null;

        /// <summary>Every filter that is on, search included, as a chip under the page header.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasActiveChips))]
        private List<ActiveFilterChip> activeChips = [];

        public bool HasActiveChips => ActiveChips.Count > 0;

        private List<ActiveFilterChip> BuildChips()
        {
            List<ActiveFilterChip> chips = [];
            if (!string.IsNullOrWhiteSpace(SearchText))
                chips.Add(new(ActiveFilterKind.Search, $"“{SearchText.Trim()}”"));
            if (IsTypeActive)
                chips.Add(new(ActiveFilterKind.Type, SelectedType!.Name));
            if (IsCategoryActive)
                chips.Add(new(ActiveFilterKind.Category, SelectedCategory!.Name));
            if (IsAccountActive)
                chips.Add(new(ActiveFilterKind.Account, SelectedAccount!.Name));
            if (IsTagActive)
                chips.Add(new(ActiveFilterKind.Tag, SelectedTag!));
            if (IsAmountActive)
                chips.Add(new(ActiveFilterKind.Amount, AmountChipText));
            return chips;
        }

        /// <summary>A chip's cross: that filter goes off, and the list follows at once.</summary>
        [RelayCommand]
        private void RemoveChip(ActiveFilterChip? chip)
        {
            if (chip == null)
                return;

            switch (chip.Kind)
            {
                case ActiveFilterKind.Search:
                    SearchText = string.Empty;
                    break;
                case ActiveFilterKind.Type:
                    SelectedType = Types[0];
                    break;
                case ActiveFilterKind.Category:
                    SelectedCategory = null;
                    break;
                case ActiveFilterKind.Account:
                    SelectedAccount = null;
                    break;
                case ActiveFilterKind.Tag:
                    SelectedTag = null;
                    break;
                case ActiveFilterKind.Amount:
                    MinAmountText = string.Empty;
                    MaxAmountText = string.Empty;
                    break;
            }

            // Typed values (search, amount) wait for a pause when edited; a removal should not
            RaiseChanged();
        }

        /// <summary>How many filters narrow the list (type, category, account, tag, amount); the search text is not counted.</summary>
        public int ActiveCount => new[] { IsTypeActive, IsCategoryActive, IsAccountActive, IsTagActive, IsAmountActive }.Count(on => on);

        public TransactionFilter ToFilter(bool ascending) => new(
            SearchText,
            SelectedType?.Type,
            SelectedCategory?.Id,
            SelectedAccount?.Id,
            ParseAmountLimit(MinAmountText),
            ParseAmountLimit(MaxAmountText),
            ascending,
            SelectedTag);

        /// <summary>The category, account and tag lists offer what the loaded entries use.</summary>
        public void SetOptions(IReadOnlyCollection<LedgerEntry> entries)
        {
            Categories = LedgerFilter.CategoryOptions(entries);
            Accounts = LedgerFilter.AccountOptions(entries);
            Tags = LedgerFilter.TagOptions(entries);
        }

        /// <summary>Empty = no limit; otherwise read like amount entry (base currency).</summary>
        private static decimal? ParseAmountLimit(string text) =>
            string.IsNullOrWhiteSpace(text) ? null : AmountParser.Parse(text, null);

        private static string FormatLimit(decimal value) => value.ToString("#,0.##", CultureInfo.CurrentCulture);

        [RelayCommand]
        private async Task PickTypeAsync()
        {
            if (await PickAsync(AppResources.FilterByType, Types.Select(t => t.Name)) is int index)
            {
                SelectedType = Types[index];
                Picked?.Invoke();
            }
        }

        [RelayCommand]
        private async Task PickCategoryAsync()
        {
            if (await PickAsync(AppResources.FilterByCategory, [AppResources.AllCategories, .. Categories.Select(c => c.Name)]) is int index)
            {
                SelectedCategory = index == 0 ? null : Categories[index - 1];
                Picked?.Invoke();
            }
        }

        [RelayCommand]
        private async Task PickAccountAsync()
        {
            if (await PickAsync(AppResources.FilterByAccount, [AppResources.AllAccounts, .. Accounts.Select(a => a.Name)]) is int index)
            {
                SelectedAccount = index == 0 ? null : Accounts[index - 1];
                Picked?.Invoke();
            }
        }

        [RelayCommand]
        private async Task PickTagAsync()
        {
            if (await PickAsync(AppResources.FilterByTag, [AppResources.AllTags, .. Tags]) is int index)
            {
                SelectedTag = index == 0 ? null : Tags[index - 1];
                Picked?.Invoke();
            }
        }

        /// <summary>The index of the picked option; null when cancelled.</summary>
        private async Task<int?> PickAsync(string title, IEnumerable<string> options)
        {
            // The list answers with the picked text, so two options never share one
            var labels = PickList.UniqueLabels(options);
            var choice = await _dialogService.ActionSheetAsync(title, AppResources.Cancel, labels);
            var index = choice == null ? -1 : labels.IndexOf(choice);
            return index < 0 ? null : index;
        }

        [RelayCommand]
        private void Clear() => ShowOnly(null, null);

        /// <summary>
        /// Every other filter off, then only <paramref name="type"/> (null = all types) and <paramref name="categoryId"/>
        /// (null = all; ignored when no entry has it): a report's drill-down. Call it after <see cref="SetOptions"/>.
        /// </summary>
        public void ShowOnly(TransactionType? type, int? categoryId)
        {
            _clearing = true;
            try
            {
                SearchText = string.Empty;
                SelectedType = Types.FirstOrDefault(t => t.Type == type) ?? Types[0];
                SelectedCategory = Categories.FirstOrDefault(c => c.Id == categoryId);
                SelectedAccount = null;
                SelectedTag = null;
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

        partial void OnSelectedTagChanged(string? value) => RaiseChanged();

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
            OnPropertyChanged(nameof(ActiveCount));
            ActiveChips = BuildChips();
            Changed?.Invoke();
        }
    }
}
