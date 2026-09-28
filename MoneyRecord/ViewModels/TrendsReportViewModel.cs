using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Controls;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    public partial class TrendsReportViewModel : ObservableObject
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IErrorHandler _errorHandler;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private BarChartData? monthlyChartData;

        [ObservableProperty]
        private BarChartData? incomeVsExpenseChartData;

        [ObservableProperty]
        private List<PieChartSlice>? categoryPieSlices;

        [ObservableProperty]
        private bool hasData = false;

        [ObservableProperty]
        private int selectedMonthCount = 6;

        public List<int> MonthCountOptions { get; } = new() { 3, 6, 12 };

        public TrendsReportViewModel(ITransactionRepository transactionRepository, ICategoryRepository categoryRepository, IErrorHandler errorHandler)
        {
            _transactionRepository = transactionRepository;
            _categoryRepository = categoryRepository;
            _errorHandler = errorHandler;
        }

        public async Task InitializeAsync()
        {
            await LoadChartsAsync();
        }

        partial void OnSelectedMonthCountChanged(int value)
        {
            _ = LoadChartsAsync();
        }

        [RelayCommand]
        private async Task LoadChartsAsync()
        {
            try
            {
                IsLoading = true;

                var now = DateTime.Now;
                var months = SelectedMonthCount;
                var startDate = new DateTime(now.Year, now.Month, 1).AddMonths(-(months - 1));
                var endDate = new DateTime(now.Year, now.Month, 1).AddMonths(1).AddTicks(-1);

                var transactions = await _transactionRepository.GetByDateRangeAsync(startDate, endDate);
                HasData = transactions.Count > 0;

                if (!HasData) return;

                BuildMonthlySpendingChart(transactions, startDate, months);
                BuildIncomeVsExpenseChart(transactions, startDate, months);
                await BuildCategoryPieChartAsync(transactions);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadReportData);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void BuildMonthlySpendingChart(List<Models.Transaction> transactions, DateTime startDate, int months)
        {
            var expensesByMonth = new decimal[months];
            var labels = new string[months];

            for (int i = 0; i < months; i++)
            {
                var monthStart = startDate.AddMonths(i);
                var monthEnd = monthStart.AddMonths(1).AddTicks(-1);
                labels[i] = monthStart.ToString("MMM yy");

                expensesByMonth[i] = transactions
                    .Where(t => t.Type == Models.TransactionType.Expense && t.Date >= monthStart && t.Date <= monthEnd)
                    .Sum(t => t.Amount);
            }

            MonthlyChartData = new BarChartData
            {
                Labels = labels,
                Series = new[]
                {
                    new BarChartSeries
                    {
                        Name = AppResources.Expenses,
                        Values = expensesByMonth,
                        Color = Color.FromArgb("#FF7043")
                    }
                }
            };
        }

        private void BuildIncomeVsExpenseChart(List<Models.Transaction> transactions, DateTime startDate, int months)
        {
            var incomeByMonth = new decimal[months];
            var expenseByMonth = new decimal[months];
            var labels = new string[months];

            for (int i = 0; i < months; i++)
            {
                var monthStart = startDate.AddMonths(i);
                var monthEnd = monthStart.AddMonths(1).AddTicks(-1);
                labels[i] = monthStart.ToString("MMM yy");

                incomeByMonth[i] = transactions
                    .Where(t => t.Type == Models.TransactionType.Income && t.Date >= monthStart && t.Date <= monthEnd)
                    .Sum(t => t.Amount);

                expenseByMonth[i] = transactions
                    .Where(t => t.Type == Models.TransactionType.Expense && t.Date >= monthStart && t.Date <= monthEnd)
                    .Sum(t => t.Amount);
            }

            IncomeVsExpenseChartData = new BarChartData
            {
                Labels = labels,
                Series = new[]
                {
                    new BarChartSeries
                    {
                        Name = AppResources.Incomes,
                        Values = incomeByMonth,
                        Color = Color.FromArgb("#42A5F5")
                    },
                    new BarChartSeries
                    {
                        Name = AppResources.Expenses,
                        Values = expenseByMonth,
                        Color = Color.FromArgb("#EF5350")
                    }
                }
            };
        }

        private async Task BuildCategoryPieChartAsync(List<Models.Transaction> transactions)
        {
            var expenseTransactions = transactions.Where(t => t.Type == Models.TransactionType.Expense).ToList();
            if (expenseTransactions.Count == 0)
            {
                CategoryPieSlices = new List<PieChartSlice>();
                return;
            }

            var categories = await _categoryRepository.GetAllAsync();
            var categoryMap = categories.ToDictionary(c => c.Id, c => c.Name);

            var byCategory = expenseTransactions
                .GroupBy(t => t.CategoryId)
                .Select(g => new
                {
                    Name = categoryMap.TryGetValue(g.Key, out var name) ? name : "Unknown",
                    Total = g.Sum(t => t.Amount)
                })
                .OrderByDescending(x => x.Total)
                .Take(8)
                .ToList();

            var colors = new[]
            {
                Color.FromArgb("#EF5350"),
                Color.FromArgb("#42A5F5"),
                Color.FromArgb("#66BB6A"),
                Color.FromArgb("#FFB74D"),
                Color.FromArgb("#AB47BC"),
                Color.FromArgb("#26C6DA"),
                Color.FromArgb("#FFEE58"),
                Color.FromArgb("#8D6E63")
            };

            CategoryPieSlices = byCategory.Select((c, i) => new PieChartSlice
            {
                Name = c.Name,
                Value = c.Total,
                Color = colors[i % colors.Length]
            }).ToList();
        }
    }
}
