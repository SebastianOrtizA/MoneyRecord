using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using MoneyRecord.Charts;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>A category of the trend report's picker: its icon, its total in the window and whether it is the one shown.</summary>
    public sealed record TrendCategoryChoice(int Id, string Name, string DisplayIcon, string AmountText, bool IsSelected);

    /// <summary>A month of the trend report's list.</summary>
    public sealed record TrendMonthRow(int Index, string Label, string AmountText);

    /// <summary>
    /// One category month by month over the shared "last N months": a column per month with the average as a line, the
    /// highest and lowest month, and the list of months. A bar or row opens that month's entries of the category.
    /// </summary>
    public partial class CategoryTrendViewModel : MonthlyReportViewModel
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly ICategoryRepository _categoryRepository;
        private int? _categoryId;

        [ObservableProperty]
        private ISeries[] trendSeries = [];

        [ObservableProperty]
        private bool isIncome;

        [ObservableProperty]
        private IReadOnlyList<TrendCategoryChoice> choices = [];

        [ObservableProperty]
        private IReadOnlyList<TrendMonthRow> monthRows = [];

        [ObservableProperty]
        private string categoryName = string.Empty;

        [ObservableProperty]
        private string selectedIcon = string.Empty;

        [ObservableProperty]
        private bool isPickerOpen;

        [ObservableProperty]
        private string averageText = string.Empty;

        [ObservableProperty]
        private string highestText = string.Empty;

        [ObservableProperty]
        private string lowestText = string.Empty;

        public CategoryTrendViewModel(PeriodViewModel period, ITransactionRepository transactionRepository,
            ICategoryRepository categoryRepository, IAccountRepository accountRepository, IConversionService conversion,
            IErrorHandler errorHandler, INavigationService navigationService, TransactionsDrillDown drillDown, ReportExportViewModel export)
            : base(period, accountRepository, conversion, errorHandler, navigationService, drillDown, export)
        {
            _transactionRepository = transactionRepository;
            _categoryRepository = categoryRepository;
        }

        private TransactionType Type => IsIncome ? TransactionType.Income : TransactionType.Expense;

        protected override async Task BuildAsync(DateTime firstMonth, int months, CancellationToken cancellationToken)
        {
            var type = Type;
            var transactionsTask = _transactionRepository.GetByDateRangeAsync(firstMonth, firstMonth.AddMonths(months).AddTicks(-1));
            var categoriesTask = _categoryRepository.GetByTypeAsync(IsIncome ? CategoryType.Income : CategoryType.Expense);
            var accountsTask = LoadAccountsAsync();
            await Task.WhenAll(transactionsTask, categoriesTask, accountsTask);
            cancellationToken.ThrowIfCancellationRequested();

            var shown = await ShownAsync(await transactionsTask, await accountsTask);
            var (code, transactions) = (shown.CurrencyCode, shown.Transactions);

            // The categories with the most in the window first; the one shown is the chosen one, else the first
            var totals = transactions.Where(t => t.Type == type).GroupBy(t => t.CategoryId)
                .ToDictionary(g => g.Key, g => g.Sum(t => Math.Abs(t.Amount)));
            var categories = (await categoriesTask)
                .OrderByDescending(c => totals.GetValueOrDefault(c.Id)).ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            var selected = categories.FirstOrDefault(c => c.Id == _categoryId) ?? categories.FirstOrDefault();
            _categoryId = selected?.Id;
            Choices = [.. categories.Select(c => new TrendCategoryChoice(c.Id, c.Name, c.DisplayIcon,
                shown.FormatAmount(totals.GetValueOrDefault(c.Id)), c.Id == selected?.Id))];
            CategoryName = selected?.Name ?? string.Empty;
            SelectedIcon = selected?.DisplayIcon ?? string.Empty;

            var trend = CategoryTrendBuilder.Build(transactions, type, selected?.Id ?? 0, firstMonth, months);
            HasData = trend.HasData;
            HeadlineText = shown.FormatAmount(trend.Total);
            AverageText = shown.FormatAmount(trend.Average);
            HighestText = $"{trend.Highest.Label}: {shown.FormatAmount(trend.Highest.Amount)}";
            LowestText = $"{trend.Lowest.Label}: {shown.FormatAmount(trend.Lowest.Amount)}";
            var delta = trend.CurrentVsAverage;
            DeltaText = delta.TextVs(AppResources.DeltaVsAverage);
            DeltaTone = delta.ToneFor(higherIsBetter: IsIncome);

            MonthAxis = ChartFactory.LabelAxis(trend.Months.Select(m => m.Label));
            MoneyAxis = ChartFactory.MoneyAxis(code);
            int[]? ids = selected == null ? null : [selected.Id];
            var color = IsIncome ? ChartPalette.Income : ChartPalette.Expense;
            var columns = ChartFactory.Columns((month, _) => OpenMonth(month, type, ids), code,
                new ChartFactory.Line(CategoryName, trend.Months.Select(m => m.Amount), color));
            TrendSeries = [.. columns, ChartFactory.AverageLine(AppResources.TrendAverage, trend.Average, trend.Months.Count, code)];
            MonthRows = [.. trend.Months.Select((m, i) => new TrendMonthRow(i, DateFormatter.Format(m.Month, DateStyle.MonthYear),
                shown.FormatAmount(m.Amount))).Reverse()];
        }

        /// <summary>Expenses or incomes: the first category of that type is shown.</summary>
        [RelayCommand]
        private void SetType(bool income)
        {
            if (IsIncome == income)
                return;

            IsIncome = income;
            _categoryId = null;
            LoadCommand.Execute(null);
        }

        [RelayCommand]
        private void OpenPicker() => IsPickerOpen = Choices.Count > 0;

        [RelayCommand]
        private void ClosePicker() => IsPickerOpen = false;

        /// <summary>Back closes the picker first; false when it was not open.</summary>
        public bool TryClosePicker()
        {
            if (!IsPickerOpen)
                return false;

            IsPickerOpen = false;
            return true;
        }

        [RelayCommand]
        private void SelectCategory(TrendCategoryChoice? choice)
        {
            IsPickerOpen = false;
            if (choice == null || choice.Id == _categoryId)
                return;

            _categoryId = choice.Id;
            LoadCommand.Execute(null);
        }

        [RelayCommand]
        private void OpenMonthRow(TrendMonthRow? row)
        {
            if (row != null && _categoryId is int id)
                OpenMonth(row.Index, Type, [id]);
        }
    }
}
