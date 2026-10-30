using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using System.Collections.ObjectModel;

namespace MoneyRecord.ViewModels
{
    public partial class ManageCategoriesViewModel : ObservableObject
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly ICategoryService _categoryService;
        private readonly ICategoryIconService _categoryIconService;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsExpenseType))]
        private CategoryType categoryType = CategoryType.Expense;

        public bool IsExpenseType => CategoryType == CategoryType.Expense;

        // Every category of the shown type; <Categories> is what the search leaves of them
        private List<Category> _all = [];

        [ObservableProperty]
        private ObservableCollection<Category> categories = new();

        [ObservableProperty]
        private string searchText = string.Empty;

        /// <summary>The list is long enough for a search box.</summary>
        public bool ShowSearch => ListSearch.ShowsSearch(_all.Count);

        public string EmptyText => _all.Count == 0 ? AppResources.NoCategoriesYet : AppResources.NoMatchingItems;

        partial void OnSearchTextChanged(string value) => ApplySearch();

        /// <summary>The add/edit sheet is open.</summary>
        [ObservableProperty]
        private bool isFormOpen;

        /// <summary>The category the sheet edits; null when it adds a new one.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEditMode), nameof(FormTitle), nameof(SaveText))]
        private Category? editingCategory;

        /// <summary>Editing an existing category: the sheet shows Delete.</summary>
        public bool IsEditMode => EditingCategory != null;

        public string FormTitle => IsEditMode ? AppResources.EditCategory : AppResources.AddNewCategory;

        public string SaveText => IsEditMode ? AppResources.Save : AppResources.AddCategoryButton;

        [ObservableProperty]
        private string formName = string.Empty;

        [ObservableProperty]
        private string formIconCode = string.Empty;

        [ObservableProperty]
        private ObservableCollection<CategoryIcon> availableIcons = new();

        public ManageCategoriesViewModel(ICategoryRepository categoryRepository, ICategoryService categoryService, ICategoryIconService categoryIconService, IErrorHandler errorHandler, IDialogService dialogService)
        {
            _categoryRepository = categoryRepository;
            _categoryService = categoryService;
            _categoryIconService = categoryIconService;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
        }

        public async Task InitializeAsync()
        {
            LoadAvailableIcons();
            await LoadCategoriesAsync();
        }

        /// <summary>The Income / Expense switch at the top of the page.</summary>
        [RelayCommand]
        private async Task SelectCategoryTypeAsync(CategoryType type)
        {
            if (type == CategoryType)
                return;

            CloseForm();
            SearchText = string.Empty;
            CategoryType = type;
            await InitializeAsync();
        }

        private void LoadAvailableIcons()
        {
            AvailableIcons.Clear();
            var icons = _categoryIconService.GetIconsForType(CategoryType);
            foreach (var icon in icons)
            {
                AvailableIcons.Add(icon);
            }
        }

        private async Task LoadCategoriesAsync()
        {
            _all = await _categoryRepository.GetByTypeAsync(CategoryType);
            ApplySearch();
        }

        // A list too short for the search box ignores whatever was typed before it shrank
        private void ApplySearch()
        {
            var query = ShowSearch ? SearchText : string.Empty;
            Categories = new(_all.Where(c => ListSearch.Matches(c.Name, query)));
            OnPropertyChanged(nameof(ShowSearch));
            OnPropertyChanged(nameof(EmptyText));
        }

        [RelayCommand]
        private void SelectIcon(string iconCode)
        {
            FormIconCode = iconCode;
            foreach (var icon in AvailableIcons)
            {
                icon.IsSelected = icon.Code == iconCode;
            }
        }

        /// <summary>Opens the sheet for a new category of the shown type.</summary>
        [RelayCommand]
        private void AddCategory()
        {
            EditingCategory = null;
            FormName = string.Empty;
            SelectIcon(_categoryIconService.GetDefaultIconCode(CategoryType));
            IsFormOpen = true;
        }

        [RelayCommand]
        private void EditCategory(Category? category)
        {
            if (category == null)
                return;

            EditingCategory = category;
            FormName = category.Name;
            SelectIcon(category.IconCode);
            IsFormOpen = true;
        }

        [RelayCommand]
        private void CloseForm()
        {
            IsFormOpen = false;
            EditingCategory = null;
        }

        /// <summary>Closes the add/edit sheet; false when it wasn't open.</summary>
        public bool CloseTopOverlay()
        {
            if (!IsFormOpen)
                return false;

            CloseForm();
            return true;
        }

        [RelayCommand]
        private async Task SaveCategoryAsync()
        {
            if (string.IsNullOrWhiteSpace(FormName))
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterCategoryName);
                return;
            }

            var category = EditingCategory ?? new Category { Type = CategoryType };
            category.Name = FormName.Trim();
            category.IconCode = string.IsNullOrEmpty(FormIconCode)
                ? _categoryIconService.GetDefaultIconCode(CategoryType)
                : FormIconCode;

            await _categoryRepository.SaveAsync(category);
            CloseForm();
            await LoadCategoriesAsync();
        }

        /// <summary>Deletes the category in the sheet; the sheet stays open behind the questions, so cancelling returns to it.</summary>
        [RelayCommand]
        private async Task DeleteCategoryAsync()
        {
            var category = EditingCategory;
            if (category == null)
                return;

            // Trip expenses sit in it while their trip is active
            if (category.IsTravel)
            {
                await _dialogService.AlertAsync(AppResources.CannotDelete, AppResources.TravelCategoryCannotDelete);
                return;
            }

            try
            {
                var usage = await _categoryService.GetUsageAsync(category.Id);

                if (usage.NeedsReplacement)
                {
                    // Get other categories of the same type (excluding the one being deleted)
                    var availableCategories = _all.Where(c => c.Id != category.Id).ToList();

                    if (availableCategories.Count == 0)
                    {
                        await _dialogService.AlertAsync(AppResources.CannotDelete, AppResources.LastCategoryCannotDelete);
                        return;
                    }

                    // Ask user to select a replacement category
                    var categoryNames = availableCategories.Select(c => c.Name).ToArray();
                    var selectedCategory = await _dialogService.ActionSheetAsync(
                        string.Format(AppResources.CategoryHasTransactions, category.Name),
                        AppResources.Cancel,
                        categoryNames);

                    if (selectedCategory == null)
                        return;

                    // Find the selected category
                    var replacementCategory = availableCategories.FirstOrDefault(c => c.Name == selectedCategory);

                    if (replacementCategory == null)
                    {
                        await _errorHandler.HandleAsync(AppResources.Error);
                        return;
                    }

                    var message = string.Format(AppResources.ConfirmCategoryMove,
                        usage.Transactions, usage.RecurringRules, category.Name, replacementCategory.Name);
                    if (usage.Budgets > 0)
                    {
                        // One budget per category: the replacement keeps its own if it has one
                        var replacementUsage = await _categoryService.GetUsageAsync(replacementCategory.Id);
                        var budgetFate = replacementUsage.Budgets > 0
                            ? AppResources.CategoryBudgetDeleted
                            : AppResources.CategoryBudgetMoves;
                        message += "\n\n" + string.Format(budgetFate, replacementCategory.Name);
                    }

                    var confirmReassign = await _dialogService.ConfirmAsync(
                        AppResources.Confirm, message, AppResources.Yes, AppResources.Cancel);

                    if (!confirmReassign)
                        return;

                    var result = await _categoryService.DeleteAsync(category.Id, replacementCategory.Id);

                    await _dialogService.ToastAsync(string.Format(AppResources.CategoryDeletedAndTransactionsMoved, category.Name, result.TransactionsMoved, replacementCategory.Name));
                }
                else
                {
                    var message = usage.Budgets > 0
                        ? string.Format(AppResources.DeleteCategoryWithBudget, category.Name)
                        : string.Format(AppResources.DeleteCategoryConfirmation, category.Name);

                    var confirm = await _dialogService.ConfirmAsync(AppResources.Confirm, message, AppResources.Yes, AppResources.No);

                    if (!confirm)
                        return;

                    await _categoryService.DeleteAsync(category.Id, replacementId: null);
                }

                CloseForm();
                await LoadCategoriesAsync();
            }
            catch (InvalidOperationException ex)
            {
                // A service guard fired because the data changed after the usage check
                await _errorHandler.HandleAsync(ex, string.Format(AppResources.DeleteFailedDataChanged, category.Name));
                CloseForm();
                await LoadCategoriesAsync();
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToDeleteCategory);
            }
        }
    }
}
