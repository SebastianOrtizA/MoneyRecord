# MoneyRecord - Personal Finance Tracker

A .NET 10 MAUI application for tracking income and expenses with real-time balance calculations.

## Features

### 1. Main Dashboard
- **Current Balance Display**: Shows real-time balance based on selected period
- **Total Incomes**: Sum of all income transactions for the period
- **Total Expenses**: Sum of all expense transactions for the period
- **Period Selection**: View data for:
  - Last Week
  - Last Month (default)
  - Last Year
  - Custom Period

### 2. Transaction Management
- **Add Income**: Record income transactions with date, category, amount, and description
- **Add Expense**: Record expense transactions with date, category, amount, and description
- **Transaction List**: View all transactions for selected period with:
  - Date
  - Description
  - Amount
  - Category
  - Sort by date (ascending/descending)
  - Color-coded by type (green for income, red for expenses)

### 3. Category Management
- **Income Categories**: Create and manage income categories
- **Expense Categories**: Create and manage expense categories
- **Default Categories**: App comes with pre-configured categories:
  - Income: Salary, Freelance, Investment, Other Income
  - Expense: Food, Transportation, Entertainment, Utilities, Shopping, Other Expense
- **Category Operations**: Add new categories, delete existing ones (swipe to delete)

### 4. Data Storage
- **SQLite Database**: All data stored locally using sqlite-net-pcl
- **Persistent Storage**: Data saved in app's local directory
- **Automatic Updates**: Balance updates automatically when transactions are added

## Technology Stack

- **.NET 10**: Latest .NET version
- **MAUI**: Cross-platform UI framework
- **SQLite**: Local database (sqlite-net-pcl - MIT License)
- **CommunityToolkit.Mvvm**: MVVM implementation (MIT License)
- **MVVM Pattern**: Clean separation of concerns

## Project Structure

```
MoneyRecord/
├── Models/
│   ├── Category.cs          - Category entity
│   └── Transaction.cs       - Transaction entity
├── Services/
│   └── DatabaseService.cs   - SQLite database operations
├── ViewModels/
│   ├── MainViewModel.cs                - Main page logic
│   ├── AddTransactionViewModel.cs      - Add transaction logic
│   └── ManageCategoriesViewModel.cs    - Category management logic
├── Views/
│   ├── MainPage.xaml/.cs               - Main dashboard
│   ├── AddTransactionPage.xaml/.cs     - Add transaction screen
│   └── ManageCategoriesPage.xaml/.cs   - Category management screen
├── Converters/
│   └── Converters.cs        - UI value converters
├── App.xaml/.cs             - App entry point
├── AppShell.xaml/.cs        - Shell navigation
└── MauiProgram.cs           - Dependency injection setup
```

## How to Use

### Adding a Transaction
1. From the main page, tap "Add Income" or "Add Expense"
2. Select the date (defaults to today)
3. Enter a description (optional)
4. Enter the amount
5. Select a category from the dropdown
6. Tap "Save"

### Managing Categories
1. Open the flyout menu (hamburger icon)
2. Select "Income Categories" or "Expense Categories"
3. Enter a new category name
4. Tap "Add Category"
5. To delete: Swipe left on a category and tap "Delete"

### Viewing Different Periods
1. On the main page, use the Period picker
2. Select "Last Week", "Last Month", "Last Year", or "Custom Period"
3. The balance, incomes, expenses, and transaction list update automatically

### Sorting Transactions
- Tap the sort button (↓ Newest First / ↑ Oldest First) to toggle sorting order

## Database Schema

### Category Table
- Id (int, primary key, auto-increment)
- Name (string)
- Type (enum: Income/Expense)

### Transaction Table
- Id (int, primary key, auto-increment)
- Date (DateTime)
- Description (string)
- Amount (decimal)
- CategoryId (int, foreign key)
- Type (enum: Income/Expense)

## License Information

All third-party packages use permissive licenses:
- **sqlite-net-pcl**: MIT License
- **SQLitePCLRaw.bundle_green**: Apache 2.0 License
- **CommunityToolkit.Mvvm**: MIT License
- **Microsoft.Maui.Controls**: MIT License

## Future Enhancements (Optional)

- Export data to CSV/Excel
- Charts and analytics
- Budget tracking
- Recurring transactions
- Multi-currency support
- Cloud backup/sync
- Receipt photo attachment
