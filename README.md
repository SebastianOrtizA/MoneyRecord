# MoneyRecord - Personal Finance Tracker

A .NET 10 MAUI application for tracking income and expenses with real-time balance calculations, multiple account support, transfers between accounts, and detailed financial reports.

## Features

### 1. Main Dashboard
- **Total Balance Display**: Shows combined balance across all accounts (tap to view individual account balances)
- **Total Incomes**: Sum of all income transactions for the period
- **Total Expenses**: Sum of all expense transactions for the period
- **Period Selection**: View data for:
  - Calendar Month (default) - Current calendar month
  - Calendar Year - Current calendar year
  - Last Week
  - Last Month
  - Last Year
  - Custom Period

### 2. Account Management
- **Multiple Accounts**: Create and manage different accounts such as:
  - Cash (default)
  - Savings Account
  - Credit Card
  - Bank Account
  - Custom accounts
- **Account Icons**: Select from Material Design icons for visual identification
- **Account Creation**: Specify name, icon, and initial balance when creating accounts
- **Negative Initial Balance**: Support for negative initial balance values (useful for credit cards or debts)
- **Default Account**: "Cash" account is created automatically as the default account
- **Account Operations**: Add, edit, and delete accounts
- **Balance Overview**: Tap the total balance on the main screen to see all account balances with last transaction dates
- **Transaction Reassignment**: When deleting an account, transactions are automatically moved to the Cash account

### 3. Transaction Management
- **Add Income**: Record income transactions with date, category, amount, description, and account
- **Add Expense**: Record expense transactions with date, category, amount, description, and account
- **Required Fields**: All transaction fields are required (date, description, amount, account, category)
- **Account Selection**: Choose which account each transaction affects
- **Currency Mask**: Amount input with automatic currency formatting
- **Transaction List**: View all transactions for selected period with:
  - Date
  - Description
  - Amount
  - Category
  - Sort by date (ascending/descending)
  - **Grouped View (Default)**: Collapsible groups by category
    - Groups are collapsed by default
    - Tap group header to expand/collapse
    - Shows category name, total amount, and transaction count
  - **List View**: All transactions in chronological order
  - Color-coded by type (green for income, red for expenses)
- **Transaction Actions**:
  - Each transaction card has Edit and Delete buttons
  - Tap "✏️ Edit" to modify the transaction
  - Tap "🗑️ Delete" to remove (with confirmation)
- **Pull to Refresh**: Pull down to reload transaction data

### 4. Transfers Between Accounts
- **Account Transfers**: Move money between accounts
- **Transfer Details**: Specify source account, destination account, amount, date, and description
- **Transfer History**: View all transfers with source and destination account names
- **Transfer Management**: Edit and delete existing transfers

### 5. Category Management
- **Income Categories**: Create and manage income categories with icons
- **Expense Categories**: Create and manage expense categories with icons
- **Category Icons**: Select from Material Design icons for visual identification
- **Default Categories**: App comes with pre-configured categories:
  - Income: Salary, Freelance, Investment, Other Income
  - Expense: Food, Transportation, Entertainment, Utilities, Shopping, Other Expense
- **Category Operations**: Add new categories with custom icons, delete existing ones (swipe to delete)

### 6. Financial Reports
- **Income Report**: Visual heatmap showing income distribution by category
  - Color-coded cells (green theme) indicating income intensity
  - Period selection for custom date ranges
  - Categories below threshold grouped into "Others"
- **Expense Report**: Visual heatmap showing expense distribution by category
  - Color-coded cells (red theme) indicating expense intensity
  - Period selection for custom date ranges
  - Categories below threshold grouped into "Others"

### 7. Budget Management
- **Category Budgets**: Set spending limits for expense categories
- **Budget Periods**: Define budgets as daily, monthly, or yearly limits
- **Budget Projection**: Automatic limit projection based on selected period
  - A daily budget of $10 shows as $310 when viewing a full month
  - A monthly budget automatically scales for yearly views
- **Progress Tracking**: Visual progress bars showing spending vs. budget limit
- **Color-coded Status**: 
  - Green: Under 60% of budget
  - Yellow: 60-80% of budget
  - Orange: 80-100% of budget
  - Red: Over budget
- **Summary View**: Total budgeted, spent, and remaining amounts
- **Over-budget Alerts**: Clear indication when spending exceeds limits

### 8. Localization
- **Multi-language Support**: Automatic language detection based on system settings
- **Supported Languages**:
  - English (default)
  - Spanish (es)
- **Dynamic Language Switching**: UI updates automatically based on system language

### 9. Android-Specific Features
- **Back Button Handling**: Custom back button behavior using OnBackPressedDispatcher
  - From any page, back button navigates to Main Page
  - From Main Page, back button closes the app
- **Shell Navigation**: Consistent navigation experience with Shell routing

### 10. Floating Action Menu (FAB)
- **Draggable FAB**: Floating action button that can be dragged anywhere on screen
- **Position Persistence**: FAB position is saved and restored between app sessions
- **Side Switching**: FAB automatically aligns to left or right edge based on drag position
- **Smooth Animations**: Uses interpolated smooth drag animations to eliminate flickering
- **Quick Actions**: Expandable menu with quick access to:
  - Add Income (blue button)
  - Add Transfer (orange button)
  - Add Expense (red button)
- **Overlay Dismissal**: Tap outside the expanded menu to close it
- **Modular Architecture**: Clean separation of concerns with:
  - `FabConfiguration`: Centralized configuration constants
  - `FabDragHandler`: Drag state tracking and velocity calculation
  - `FabPositionManager`: Position calculations and persistence
  - `FabLayoutManager`: UI alignment updates
  - `SmoothDragAnimator`: Smooth position interpolation during drag

### 11. Data Storage
- **SQLite Database**: All data stored locally using sqlite-net-pcl
- **Persistent Storage**: Data saved in app's local directory
- **Automatic Updates**: Balance updates automatically when transactions are added or modified

## Technology Stack

- **.NET 10**: Latest .NET version
- **MAUI**: Cross-platform UI framework (Android, iOS, macOS, Windows)
- **SQLite**: Local database (sqlite-net-pcl - MIT License)
- **CommunityToolkit.Mvvm**: MVVM implementation (MIT License)
- **Material Design Icons**: Category and account icons (MIT/Apache 2.0 License)
- **MVVM Pattern**: Clean separation of concerns

## Project Structure

```
MoneyRecord/
├── Behaviors/
│   └── CurrencyMaskBehavior.cs   - Currency input formatting
├── Controls/
│   ├── FabComponents/
│   │   ├── FabConfiguration.cs       - FAB configuration constants
│   │   ├── FabDragHandler.cs         - Drag state and velocity tracking
│   │   ├── FabLayoutManager.cs       - UI alignment management
│   │   ├── FabPositionManager.cs     - Position calculations and persistence
│   │   └── SmoothDragAnimator.cs     - Smooth drag interpolation
│   └── FloatingActionMenu.xaml/.cs   - Draggable floating action menu
├── Converters/
│   └── Converters.cs             - UI value converters
├── Extensions/
│   └── LocalizeExtension.cs      - XAML localization extension
├── Helpers/
│   ├── BudgetProjectionHelper.cs - Budget limit projection calculations
│   ├── DateRangeHelper.cs        - Date range utilities
│   └── PeriodHelper.cs           - Period calculation utilities
├── Models/
│   ├── Account.cs                - Account entity
│   ├── AccountBalanceInfo.cs     - Account balance display model
│   ├── Budget.cs                 - Budget entity and progress model
│   ├── Category.cs               - Category entity
│   ├── CategoryReport.cs         - Report data model
│   ├── PeriodType.cs             - Period type enumeration
│   ├── Transaction.cs            - Transaction entity
│   ├── TransactionGroup.cs       - Grouped transactions model
│   └── Transfer.cs               - Transfer between accounts entity
├── Platforms/
│   ├── Android/
│   │   └── MainActivity.cs       - Android-specific back button handling
│   └── MacCatalyst/
│       ├── AppDelegate.cs        - macOS app delegate
│       └── Program.cs            - macOS entry point
├── Resources/
│   └── Strings/
│       ├── AppResources.resx     - English strings
│       └── AppResources.es.resx  - Spanish strings
├── Services/
│   ├── CategoryIconService.cs    - Icon management for categories/accounts
│   ├── DatabaseService.cs        - SQLite database operations
│   └── LocalizationService.cs    - Language management
├── ViewModels/
│   ├── AddTransactionViewModel.cs      - Add/edit transaction logic
│   ├── AddTransferViewModel.cs         - Add/edit transfer logic
│   ├── BudgetViewModel.cs              - Budget management logic
│   ├── ExpenseReportViewModel.cs       - Expense report logic
│   ├── IncomeReportViewModel.cs        - Income report logic
│   ├── MainViewModel.cs                - Main page logic
│   ├── ManageAccountsViewModel.cs      - Account management logic
│   ├── ManageCategoriesViewModel.cs    - Category management logic
│   └── TransfersViewModel.cs           - Transfer list logic
├── Views/
│   ├── AddTransactionPage.xaml/.cs     - Add/edit transaction screen
│   ├── AddTransferPage.xaml/.cs        - Add/edit transfer screen
│   ├── BudgetPage.xaml/.cs             - Budget management screen
│   ├── ExpenseReportPage.xaml/.cs      - Expense report screen
│   ├── IncomeReportPage.xaml/.cs       - Income report screen
│   ├── MainPage.xaml/.cs               - Main dashboard
│   ├── ManageAccountsPage.xaml/.cs     - Account management screen
│   ├── ManageCategoriesPage.xaml/.cs   - Category management screen
│   └── TransfersPage.xaml/.cs          - Transfer list screen
├── App.xaml/.cs              - App entry point
├── AppShell.xaml/.cs         - Shell navigation with back button handling
└── MauiProgram.cs            - Dependency injection setup
```

## License

This project is open source. Third-party libraries used:
- **sqlite-net-pcl**: MIT License
- **CommunityToolkit.Mvvm**: MIT License
- **Material Design Icons**: MIT/Apache 2.0 License
