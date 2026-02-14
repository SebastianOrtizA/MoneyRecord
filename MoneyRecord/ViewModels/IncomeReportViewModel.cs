using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services;
using System.Collections.ObjectModel;

namespace MoneyRecord.ViewModels
{
    public partial class IncomeReportViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        private const double OthersThreshold = 10.0; // Categories below 10% go into "Others"

        // Store income transactions for detail popup
        private List<Transaction> _incomeTransactions = new();

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
        private decimal totalIncome;

        [ObservableProperty]
        private bool hasData = false;

        [ObservableProperty]
        private ObservableCollection<HeatmapCell> heatmapCells = new();

        [ObservableProperty]
        private ObservableCollection<CategoryIncomeData> categoryDetails = new();

        public List<PeriodItem> Periods { get; } = PeriodHelper.GetReportPeriods();

        // Heatmap color palette (from coolest to hottest - green/blue theme for income)
        private static readonly Color[] HeatmapColors = new[]
        {
            Color.FromArgb("#E8F5E9"), // Very light green (lowest)
            Color.FromArgb("#C8E6C9"), // Light green
            Color.FromArgb("#A5D6A7"), // Medium light green
            Color.FromArgb("#81C784"), // Green
            Color.FromArgb("#66BB6A"), // Medium green
            Color.FromArgb("#4CAF50"), // Strong green
            Color.FromArgb("#43A047"), // Dark green
            Color.FromArgb("#388E3C"), // Deeper green
            Color.FromArgb("#2E7D32"), // Deep green
            Color.FromArgb("#1B5E20"), // Very deep green
            Color.FromArgb("#00796B"), // Teal
            Color.FromArgb("#004D40"), // Dark teal (highest)
        };

        public IncomeReportViewModel(DatabaseService databaseService)
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

                // Get all income transactions in the date range
                var transactions = await _databaseService.GetTransactionsAsync(startDate, endDate);
                _incomeTransactions = transactions
                    .Where(t => t.Type == TransactionType.Income)
                    .ToList();

                TotalIncome = _incomeTransactions.Sum(t => Math.Abs(t.Amount));
                HasData = TotalIncome > 0;

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
                var categoryGroups = _incomeTransactions
                    .GroupBy(t => new { t.CategoryId, t.CategoryName, t.CategoryIconCode })
                    .Select(g => new CategoryIncomeData
                    {
                        CategoryId = g.Key.CategoryId,
                        CategoryName = g.Key.CategoryName ?? "Unknown",
                        IconCode = g.Key.CategoryIconCode ?? "F0770",
                        Amount = g.Sum(t => Math.Abs(t.Amount)),
                        Percentage = (double)(g.Sum(t => Math.Abs(t.Amount)) / TotalIncome * 100),
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
                    await Shell.Current.DisplayAlertAsync("Error", $"Failed to load report data: {ex.Message}", "OK");
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
            await ShowCategoryIncomeDetails(cell.CategoryId, cell.CategoryName);
        }

        [RelayCommand]
        private async Task ShowCategoryDetailsFromList(CategoryIncomeData category)
        {
            if (category == null) return;
            await ShowCategoryIncomeDetails(category.CategoryId, category.CategoryName);
        }

        private async Task ShowCategoryIncomeDetails(int categoryId, string categoryName)
        {
            try
            {
                var categoryTransactions = _incomeTransactions
                    .Where(t => t.CategoryId == categoryId)
                    .OrderByDescending(t => t.Date)
                    .ToList();

                if (!categoryTransactions.Any())
                {
                    await Shell.Current.DisplayAlertAsync(categoryName, "No income details found.", "OK");
                    return;
                }

                // Build the details message
                var details = categoryTransactions.Select(t =>
                    $"📅 {t.Date:MMM dd, yyyy}\n" +
                    $"   💰 ${Math.Abs(t.Amount):N2}\n" +
                    $"   📝 {(string.IsNullOrEmpty(t.Description) ? "No description" : t.Description)}"
                );

                var totalAmount = categoryTransactions.Sum(t => Math.Abs(t.Amount));
                var transactionCount = categoryTransactions.Count;

                var message = $"Total: ${totalAmount:N2} ({transactionCount} transaction{(transactionCount > 1 ? "s" : "")})\n\n" +
                             string.Join("\n\n", details);

                await Shell.Current.DisplayAlertAsync($"💵 {categoryName}", message, "Close");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error", $"Failed to load category details: {ex.Message}", "OK");
            }
        }

        private void AssignHeatmapColors(List<CategoryIncomeData> categories)
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
