using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

        [ObservableProperty]
        private ObservableCollection<Category> categories = new();

        [ObservableProperty]
        private string newCategoryName = string.Empty;

        [ObservableProperty]
        private string newCategoryIconCode = string.Empty;

        [ObservableProperty]
        private string title = string.Empty;

        [ObservableProperty]
        private bool isEditMode = false;

        [ObservableProperty]
        private Category? editingCategory;

        [ObservableProperty]
        private string editCategoryName = string.Empty;

        [ObservableProperty]
        private string editCategoryIconCode = string.Empty;

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
            Title = CategoryType == CategoryType.Income ? AppResources.ManageIncomeCategories : AppResources.ManageExpenseCategories;
            LoadAvailableIcons();
            NewCategoryIconCode = _categoryIconService.GetDefaultIconCode(CategoryType);
            UpdateIconSelection(NewCategoryIconCode);
            await LoadCategoriesAsync();
        }

        /// <summary>The Income / Expense switch at the top of the page.</summary>
        [RelayCommand]
        private async Task SelectCategoryTypeAsync(CategoryType type)
        {
            if (type == CategoryType)
                return;

            CancelEdit();
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
            var categoryList = await _categoryRepository.GetByTypeAsync(CategoryType);
            Categories.Clear();
            foreach (var category in categoryList)
            {
                Categories.Add(category);
            }
        }

        private void UpdateIconSelection(string selectedCode)
        {
            foreach (var icon in AvailableIcons)
            {
                icon.IsSelected = icon.Code == selectedCode;
            }
        }

        [RelayCommand]
        private void SelectNewIcon(string iconCode)
        {
            NewCategoryIconCode = iconCode;
            UpdateIconSelection(iconCode);
        }

        [RelayCommand]
        private void SelectEditIcon(string iconCode)
        {
            EditCategoryIconCode = iconCode;
            UpdateIconSelection(iconCode);
        }

        [RelayCommand]
        private async Task AddCategoryAsync()
        {
            if (string.IsNullOrWhiteSpace(NewCategoryName))
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterCategoryName);
                return;
            }

            var category = new Category
            {
                Name = NewCategoryName,
                Type = CategoryType,
                IconCode = string.IsNullOrEmpty(NewCategoryIconCode) 
                    ? _categoryIconService.GetDefaultIconCode(CategoryType) 
                    : NewCategoryIconCode
            };

            await _categoryRepository.SaveAsync(category);
            NewCategoryName = string.Empty;
            NewCategoryIconCode = _categoryIconService.GetDefaultIconCode(CategoryType);
            UpdateIconSelection(NewCategoryIconCode);
            await LoadCategoriesAsync();
        }

        [RelayCommand]
        private async Task DeleteCategoryAsync(Category category)
        {
            try
            {
                var usage = await _categoryService.GetUsageAsync(category.Id);

                if (usage.NeedsReplacement)
                {
                    // Get other categories of the same type (excluding the one being deleted)
                    var availableCategories = Categories.Where(c => c.Id != category.Id).ToList();

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

                await LoadCategoriesAsync();
            }
            catch (InvalidOperationException ex)
            {
                // A service guard fired because the data changed after the usage check
                await _errorHandler.HandleAsync(ex, string.Format(AppResources.DeleteFailedDataChanged, category.Name));
                await LoadCategoriesAsync();
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToDeleteCategory);
            }
        }

        [RelayCommand]
        private void EditCategory(Category category)
        {
            EditingCategory = category;
            EditCategoryName = category.Name;
            EditCategoryIconCode = category.IconCode;
            UpdateIconSelection(category.IconCode);
            IsEditMode = true;
        }

        [RelayCommand]
        private async Task SaveEditAsync()
        {
            if (EditingCategory == null)
                return;

            if (string.IsNullOrWhiteSpace(EditCategoryName))
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterCategoryName);
                return;
            }

            EditingCategory.Name = EditCategoryName;
            EditingCategory.IconCode = string.IsNullOrEmpty(EditCategoryIconCode)
                ? _categoryIconService.GetDefaultIconCode(CategoryType)
                : EditCategoryIconCode;
            
            await _categoryRepository.SaveAsync(EditingCategory);

            IsEditMode = false;
            EditingCategory = null;
            EditCategoryName = string.Empty;
            EditCategoryIconCode = string.Empty;
            UpdateIconSelection(NewCategoryIconCode);

            await LoadCategoriesAsync();
        }

        [RelayCommand]
        private void CancelEdit()
        {
            IsEditMode = false;
            EditingCategory = null;
            EditCategoryName = string.Empty;
            EditCategoryIconCode = string.Empty;
            UpdateIconSelection(NewCategoryIconCode);
        }
    }
}
