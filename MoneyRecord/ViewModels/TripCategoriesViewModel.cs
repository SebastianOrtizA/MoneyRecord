using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>Trip categories (Lodging, Flights...): a list, and an add/edit sheet with the icon grid.</summary>
    public partial class TripCategoriesViewModel : ObservableObject
    {
        private readonly ITripService _trips;
        private readonly ICategoryIconService _icons;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private List<TripCategory> categories = [];

        [ObservableProperty]
        private bool isFormOpen;

        /// <summary>The category the sheet edits; null when it adds one.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEditMode), nameof(FormTitle), nameof(SaveText))]
        private TripCategory? editingCategory;

        public bool IsEditMode => EditingCategory != null;

        public string FormTitle => IsEditMode ? AppResources.EditTripCategory : AppResources.AddTripCategory;

        public string SaveText => IsEditMode ? AppResources.Save : AppResources.AddTripCategory;

        [ObservableProperty]
        private string formName = string.Empty;

        [ObservableProperty]
        private string formIconCode = string.Empty;

        public ObservableCollection<CategoryIcon> AvailableIcons { get; } = [];

        public TripCategoriesViewModel(ITripService trips, ICategoryIconService icons, IErrorHandler errorHandler, IDialogService dialogService)
        {
            _trips = trips;
            _icons = icons;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
            foreach (var icon in _icons.GetExpenseIcons())
                AvailableIcons.Add(icon);
        }

        public Task InitializeAsync() => LoadAsync();

        private async Task LoadAsync()
        {
            try
            {
                Categories = await _trips.GetCategoriesAsync();
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadTrips);
            }
        }

        [RelayCommand]
        private void SelectIcon(string iconCode)
        {
            FormIconCode = iconCode;
            foreach (var icon in AvailableIcons)
                icon.IsSelected = icon.Code == iconCode;
        }

        [RelayCommand]
        private void AddCategory()
        {
            EditingCategory = null;
            FormName = string.Empty;
            SelectIcon(_icons.GetDefaultIconCode(CategoryType.Expense));
            IsFormOpen = true;
        }

        [RelayCommand]
        private void EditCategory(TripCategory? category)
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

        /// <summary>Closes the sheet; false when it wasn't open.</summary>
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
            var name = FormName.Trim();
            if (name.Length == 0)
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterCategoryName);
                return;
            }

            try
            {
                var category = EditingCategory ?? new TripCategory();
                category.Name = name;
                category.IconCode = string.IsNullOrEmpty(FormIconCode) ? _icons.GetDefaultIconCode(CategoryType.Expense) : FormIconCode;
                await _trips.SaveCategoryAsync(category);
                CloseForm();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToSaveTripCategory);
            }
        }

        /// <summary>Deletes the category in the sheet; one that is in use first asks where its expenses and limits go.</summary>
        [RelayCommand]
        private async Task DeleteCategoryAsync()
        {
            var category = EditingCategory;
            if (category == null)
                return;

            try
            {
                if (Categories.Count <= 1)
                {
                    await _dialogService.AlertAsync(AppResources.CannotDelete, AppResources.LastTripCategoryCannotDelete);
                    return;
                }

                var others = Categories.Where(c => c.Id != category.Id).ToList();
                int? replacementId = null;
                if (await _trips.CountCategoryUsesAsync(category.Id) > 0)
                {
                    var labels = PickList.UniqueLabels(others.Select(c => c.Name));
                    var picked = await _dialogService.ActionSheetAsync(
                        string.Format(AppResources.TripCategoryInUse, category.Name), AppResources.Cancel, labels);
                    var index = picked == null ? -1 : labels.IndexOf(picked);
                    if (index < 0)
                        return;
                    replacementId = others[index].Id;
                }
                else if (!await _dialogService.ConfirmAsync(AppResources.ConfirmDelete,
                             string.Format(AppResources.DeleteTripCategoryConfirmation, category.Name), AppResources.YesDelete, AppResources.Cancel))
                {
                    return;
                }

                await _trips.DeleteCategoryAsync(category.Id, replacementId);
                CloseForm();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToDeleteTripCategory);
            }
        }
    }
}
