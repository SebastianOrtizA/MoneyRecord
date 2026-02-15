using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using System.Collections.ObjectModel;

namespace MoneyRecord.ViewModels
{
    public partial class ExpenseReportViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        private const double OthersThreshold = 10.0; // Categories below 10% go into "Others"

        // Store expense transactions for detail popup
        private List<Transaction> _expenseTransactions = new();

        [ObservableProperty]
        private PeriodItem selectedPeriod;

        [ObservableProperty]
        private DateTime customStartDate = DateTime.Now.AddMonths(-1);

        [ObservableProperty]
        private DateTime customEndDate = DateTime.Now;

        [ObservableProperty]
        private bool isCustomPeriodSelected = false;

        [ObservableProperty]
        private bool isLoading = false;

        [ObservableProperty]
        private decimal totalExpenses;

        [ObservableProperty]
        private bool hasData = false;

        [ObservableProperty]
        private ObservableCollection<HeatmapCell> heatmapCells = new();

        [ObservableProperty]
        private ObservableCollection<CategoryExpenseData> categoryDetails = new();

        public List<PeriodItem> Periods { get; } = PeriodHelper.GetReportPeriods();

        // Heatmap color palette (white to pink to red gradient)
        private static readonly Color[] HeatmapColors = new[]
        {
            Color.FromArgb("#FFFFFF"), // White (lowest)
            Color.FromArgb("#FFF5F5"), // Very light pink
            Color.FromArgb("#FFEBEE"), // Light pink
            Color.FromArgb("#FFCDD2"), // Pink
            Color.FromArgb("#EF9A9A"), // Medium pink
            Color.FromArgb("#E57373"), // Light red
            Color.FromArgb("#EF5350"), // Red
            Color.FromArgb("#F44336"), // Bright red
            Color.FromArgb("#E53935"), // Strong red
            Color.FromArgb("#D32F2F"), // Dark red
            Color.FromArgb("#C62828"), // Deeper red
            Color.FromArgb("#B71C1C"), // Deep red (highest)
        };

        public ExpenseReportViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            selectedPeriod = PeriodHelper.GetDefaultPeriod();
        }

        public async Task InitializeAsync()
        {
            await LoadReportDataAsync();
        }

        [RelayCommand]
        private async Task LoadReportDataAsync()
        {
            try
            {
                IsLoading = true;

                var (startDate, endDate) = GetDateRange();

                // Get all expense transactions in the date range
                var transactions = await _databaseService.GetTransactionsAsync(startDate, endDate);
                _expenseTransactions = transactions
                    .Where(t => t.Type == TransactionType.Expense)
                    .ToList();

                TotalExpenses = _expenseTransactions.Sum(t => Math.Abs(t.Amount));
                HasData = TotalExpenses > 0;

                if (!HasData)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        HeatmapCells.Clear();
                        CategoryDetails.Clear();
                    });
                    return;
                }

                // Group by category and calculate percentages
                var categoryGroups = _expenseTransactions
                    .GroupBy(t => new { t.CategoryId, t.CategoryName, t.CategoryIconCode })
                    .Select(g => new CategoryExpenseData
                    {
                        CategoryId = g.Key.CategoryId,
                        CategoryName = g.Key.CategoryName ?? "Unknown",
                        IconCode = g.Key.CategoryIconCode ?? "F0770",
                        Amount = g.Sum(t => Math.Abs(t.Amount)),
                        Percentage = (double)(g.Sum(t => Math.Abs(t.Amount)) / TotalExpenses * 100),
                        IsOthersGroup = false
                    })
                    .OrderByDescending(c => c.Percentage)
                    .ToList();

                // Mark categories below threshold for visual indicator
                foreach (var category in categoryGroups)
                {
                    category.IsOthersGroup = category.Percentage < OthersThreshold;
                }

                // Assign heatmap colors based on percentage ranking
                AssignHeatmapColors(categoryGroups);

                // Create heatmap cells for all categories
                var cells = categoryGroups.Select(c => new HeatmapCell
                {
                    CategoryId = c.CategoryId,
                    CategoryName = c.CategoryName,
                    DisplayIcon = c.DisplayIcon,
                    Percentage = c.Percentage,
                    Amount = c.Amount,
                    BackgroundColor = c.HeatmapColor,
                    TextColor = GetContrastColor(c.HeatmapColor),
                    IsOthersGroup = c.IsOthersGroup
                }).ToList();

                // Create detailed list (same as heatmap - all categories)
                var detailList = categoryGroups.ToList();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    HeatmapCells.Clear();
                    foreach (var cell in cells)
                    {
                        HeatmapCells.Add(cell);
                    }

                    CategoryDetails.Clear();
                    foreach (var detail in detailList)
                    {
                        CategoryDetails.Add(detail);
                    }
                });
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Shell.Current.DisplayAlertAsync(AppResources.Error, string.Format(AppResources.FailedToLoadReportData, ex.Message), AppResources.OK);
                });
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ShowCategoryDetailsFromHeatmap(HeatmapCell cell)
        {
            if (cell == null) return;
            await ShowCategoryExpenseDetails(cell.CategoryId, cell.CategoryName);
        }

        [RelayCommand]
        private async Task ShowCategoryDetailsFromList(CategoryExpenseData category)
        {
            if (category == null) return;
            await ShowCategoryExpenseDetails(category.CategoryId, category.CategoryName);
        }

        private async Task ShowCategoryExpenseDetails(int categoryId, string categoryName)
        {
            try
            {
                var categoryTransactions = _expenseTransactions
                    .Where(t => t.CategoryId == categoryId)
                    .OrderByDescending(t => t.Date)
                    .ToList();

                if (!categoryTransactions.Any())
                {
                    await Shell.Current.DisplayAlertAsync(categoryName, AppResources.NoExpenseDetailsFound, AppResources.OK);
                    return;
                }

                // Build the details message
                var details = categoryTransactions.Select(t => 
                    $"📅 {t.Date:MMM dd, yyyy}\n" +
                    $"   💰 ${Math.Abs(t.Amount):N2}\n" +
                    $"   📝 {(string.IsNullOrEmpty(t.Description) ? AppResources.NoDescription : t.Description)}"
                );

                var totalAmount = categoryTransactions.Sum(t => Math.Abs(t.Amount));
                var transactionCount = categoryTransactions.Count;

                var message = $"{AppResources.Total}: ${totalAmount:N2} ({transactionCount} {(transactionCount > 1 ? AppResources.TransactionPlural : AppResources.TransactionSingular)})\n\n" +
                             string.Join("\n\n", details);

                await Shell.Current.DisplayAlertAsync($"📊 {categoryName}", message, AppResources.Close);
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync(AppResources.Error, string.Format(AppResources.FailedToLoadCategoryDetails, ex.Message), AppResources.OK);
            }
        }

        private void AssignHeatmapColors(List<CategoryExpenseData> categories)
        {
            if (!categories.Any()) return;

            var maxPercentage = categories.Max(c => c.Percentage);
            var minPercentage = categories.Min(c => c.Percentage);
            var range = maxPercentage - minPercentage;

            foreach (var category in categories)
            {
                double normalizedValue;
                if (range > 0)
                {
                    normalizedValue = (category.Percentage - minPercentage) / range;
                }
                else
                {
                    normalizedValue = 0.5; // All same percentage
                }

                var colorIndex = (int)(normalizedValue * (HeatmapColors.Length - 1));
                colorIndex = Math.Clamp(colorIndex, 0, HeatmapColors.Length - 1);
                category.HeatmapColor = HeatmapColors[colorIndex];
            }
        }

        private static Color GetContrastColor(Color backgroundColor)
        {
            // Calculate relative luminance
            var luminance = 0.299 * backgroundColor.Red + 0.587 * backgroundColor.Green + 0.114 * backgroundColor.Blue;
            return luminance > 0.5 ? Colors.Black : Colors.White;
        }

        partial void OnSelectedPeriodChanged(PeriodItem value)
        {
            IsCustomPeriodSelected = value?.Type == PeriodType.CustomPeriod;
            _ = LoadReportDataAsync();
        }

        partial void OnCustomStartDateChanged(DateTime value)
        {
            if (IsCustomPeriodSelected)
            {
                _ = LoadReportDataAsync();
            }
        }

        partial void OnCustomEndDateChanged(DateTime value)
        {
            if (IsCustomPeriodSelected)
            {
                _ = LoadReportDataAsync();
            }
        }

        private (DateTime startDate, DateTime endDate) GetDateRange()
        {
            return DateRangeHelper.GetDateRange(SelectedPeriod?.Type, CustomStartDate, CustomEndDate);
        }
    }
}
