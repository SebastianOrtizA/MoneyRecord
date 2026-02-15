using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using System.Collections.ObjectModel;

namespace MoneyRecord.ViewModels
{
    [QueryProperty(nameof(CategoryType), "CategoryType")]
    public partial class ManageCategoriesViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly ICategoryIconService _categoryIconService;

        [ObservableProperty]
        private CategoryType categoryType;

        [ObservableProperty]
        private ObservableCollection<Category> categories = new();

        [ObservableProperty]
        private string newCategoryName = string.Empty;

        [ObservableProperty]
        private string newCategoryIconCode = string.Empty;

        [ObservableProperty]
        private string title = "Manage Categories";

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

        public ManageCategoriesViewModel(DatabaseService databaseService, ICategoryIconService categoryIconService)
        {
            _databaseService = databaseService;
            _categoryIconService = categoryIconService;
        }

        public async Task InitializeAsync()
        {
            Title = CategoryType == CategoryType.Income ? AppResources.ManageIncomeCategories : AppResources.ManageExpenseCategories;
            LoadAvailableIcons();
            NewCategoryIconCode = _categoryIconService.GetDefaultIconCode(CategoryType);
            UpdateIconSelection(NewCategoryIconCode);
            await LoadCategoriesAsync();
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
            var categoryList = await _databaseService.GetCategoriesAsync(CategoryType);
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
                await Shell.Current.DisplayAlertAsync(AppResources.Error, AppResources.PleaseEnterCategoryName, AppResources.OK);
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

            await _databaseService.SaveCategoryAsync(category);
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
                // Check if category has transactions
                var hasTransactions = await _databaseService.CategoryHasTransactionsAsync(category.Id);

                if (hasTransactions)
                {
                    // Get other categories of the same type (excluding the one being deleted)
                    var availableCategories = Categories.Where(c => c.Id != category.Id).ToList();

                    if (availableCategories.Count == 0)
                    {
                        await Shell.Current.DisplayAlertAsync(AppResources.CannotDelete, 
                            AppResources.LastCategoryCannotDelete, 
                            AppResources.OK);
                        return;
                    }

                    // Ask user to select a replacement category
                    var categoryNames = availableCategories.Select(c => c.Name).ToArray();
                    var selectedCategory = await Shell.Current.DisplayActionSheetAsync(
                        string.Format(AppResources.CategoryHasTransactions, category.Name),
                        AppResources.Cancel,
                        null,
                        categoryNames);

                    if (selectedCategory == AppResources.Cancel || selectedCategory == null)
                        return;

                    // Find the selected category
                    var replacementCategory = availableCategories.FirstOrDefault(c => c.Name == selectedCategory);
                    
                    if (replacementCategory == null)
                    {
                        await Shell.Current.DisplayAlertAsync(AppResources.Error, AppResources.Error, AppResources.OK);
                        return;
                    }

                    // Get transaction count for confirmation message
                    var transactionCount = await _databaseService.GetTransactionCountByCategoryAsync(category.Id);

                    // Confirm the reassignment
                    var confirmReassign = await Shell.Current.DisplayAlertAsync(
                        AppResources.Confirm,
                        $"{transactionCount} → '{category.Name}' → '{replacementCategory.Name}'",
                        AppResources.Yes,
                        AppResources.Cancel);

                    if (!confirmReassign)
                    {
                        return;
                    }

                    // Reassign transactions
                    var reassignedCount = await _databaseService.ReassignTransactionsCategoryAsync(category.Id, replacementCategory.Id);

                    // Delete the category
                    await _databaseService.DeleteCategoryAsync(category);

                    await Shell.Current.DisplayAlertAsync(AppResources.Success, 
                        string.Format(AppResources.CategoryDeletedAndTransactionsMoved, category.Name, reassignedCount, replacementCategory.Name), 
                        AppResources.OK);
                }
                else
                {
                    // No transactions, just confirm deletion
                    var confirm = await Shell.Current.DisplayAlertAsync(AppResources.Confirm, 
                        string.Format(AppResources.DeleteCategoryConfirmation, category.Name), AppResources.Yes, AppResources.No);
                    
                    if (!confirm)
                        return;

                    await _databaseService.DeleteCategoryAsync(category);
                }

                await LoadCategoriesAsync();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync(AppResources.Error, ex.Message, AppResources.OK);
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
                await Shell.Current.DisplayAlertAsync(AppResources.Error, AppResources.PleaseEnterCategoryName, AppResources.OK);
                return;
            }

            EditingCategory.Name = EditCategoryName;
            EditingCategory.IconCode = string.IsNullOrEmpty(EditCategoryIconCode)
                ? _categoryIconService.GetDefaultIconCode(CategoryType)
                : EditCategoryIconCode;
            
            await _databaseService.SaveCategoryAsync(EditingCategory);

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

        [RelayCommand]
        private async Task GoBackAsync()
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
