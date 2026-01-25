# MoneyRecord - Personal Finance Tracker

A .NET 10 MAUI application for tracking income and expenses with real-time balance calculations and multiple account support.

## Features

### 1. Main Dashboard
- **Total Balance Display**: Shows combined balance across all accounts (tap to view individual account balances)
- **Total Incomes**: Sum of all income transactions for the period
- **Total Expenses**: Sum of all expense transactions for the period
- **Period Selection**: View data for:
  - Last Week
  - Last Month (default)
  - Last Year
  - Custom Period

### 2. Account Management
- **Multiple Accounts**: Create and manage different accounts such as:
  - Cash (default)
  - Savings Account
  - Credit Card
  - Bank Account
  - Custom accounts
- **Account Creation**: Specify name and initial balance when creating accounts
- **Default Account**: "Cash" account is created automatically as the default account
- **Account Operations**: Add, edit, and delete accounts
- **Balance Overview**: Tap the total balance on the main screen to see all account balances with last transaction dates
- **Transaction Reassignment**: When deleting an account, transactions are automatically moved to the Cash account

### 3. Transaction Management
- **Add Income**: Record income transactions with date, category, amount, description, and account
- **Add Expense**: Record expense transactions with date, category, amount, description, and account
- **Required Fields**: All transaction fields are required (date, description, amount, account, category)
- **Account Selection**: Choose which account each transaction affects
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

### 4. Category Management
- **Income Categories**: Create and manage income categories
- **Expense Categories**: Create and manage expense categories
- **Default Categories**: App comes with pre-configured categories:
  - Income: Salary, Freelance, Investment, Other Income
  - Expense: Food, Transportation, Entertainment, Utilities, Shopping, Other Expense
- **Category Operations**: Add new categories, delete existing ones (swipe to delete)

### 5. Data Storage
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
│   ├── Account.cs            - Account entity
│   ├── AccountBalanceInfo.cs - Account balance display model
│   ├── Category.cs           - Category entity
│   ├── Transaction.cs        - Transaction entity
│   └── TransactionGroup.cs   - Grouped transactions model
├── Services/
│   └── DatabaseService.cs    - SQLite database operations
├── ViewModels/
│   ├── MainViewModel.cs                - Main page logic
│   ├── AddTransactionViewModel.cs      - Add transaction logic
│   ├── ManageCategoriesViewModel.cs    - Category management logic
│   └── ManageAccountsViewModel.cs      - Account management logic
├── Views/
│   ├── MainPage.xaml/.cs               - Main dashboard
│   ├── AddTransactionPage.xaml/.cs     - Add transaction screen
│   ├── ManageCategoriesPage.xaml/.cs   - Category management screen
│   └── ManageAccountsPage.xaml/.cs     - Account management screen
├── Converters/
│   └── Converters.cs         - UI value converters
├── App.xaml/.cs              - App entry point
├── AppShell.xaml/.cs         - Shell navigation
└── MauiProgram.cs            - Dependency injection setup
```

## How to Use

### Managing Accounts
1. Open the flyout menu (hamburger icon)
2. Select "Manage Accounts"
3. To add an account:
   - Enter the account name (e.g., "Bank Account", "Cash", "Credit Card")
   - Enter the initial balance
   - Tap "➕ Add Account"
4. To edit an account: Tap the **"✏️ Edit"** button
5. To delete an account: Tap the **"🗑️ Delete"** button (transactions will be moved to General account)

### Viewing Account Balances
1. On the main page, tap the **Total Balance** card
2. A popup will show all accounts with:
   - Account name
   - Current balance
   - Last transaction date

### Adding a Transaction
1. From the main page, tap "💰 Add Income" or "💸 Add Expense"
2. Select the date (defaults to today)
3. Enter a description (optional)
4. Enter the amount
5. Select an account from the dropdown (defaults to General)
6. Select a category from the dropdown
7. Tap "Save"

### Editing a Transaction
1. On the main page, find the transaction you want to edit
2. Tap the **"✏️ Edit"** button on the transaction card
3. Make your changes in the edit screen (including changing the account)
4. Tap "Save" to save changes

### Deleting a Transaction
1. On the main page, find the transaction you want to delete
2. Tap the **"🗑️ Delete"** button on the transaction card
3. Confirm the deletion in the dialog
4. Tap "Yes, Delete" to permanently remove the transaction

### Managing Categories
1. Open the flyout menu (hamburger icon)
2. Select "Income Categories" or "Expense Categories"
3. Enter a new category name
4. Tap "Add Category"
5. To delete: Tap the delete button on a category

### Viewing Different Periods
1. On the main page, use the Period picker
2. Select "Last Week", "Last Month", "Last Year", or "Custom Period"
3. If "Custom Period" is selected, choose your start and end dates
4. The balance, incomes, expenses, and transaction list update automatically

### Sorting Transactions
- Tap the sort button (↓ Newest First / ↑ Oldest First) to toggle sorting order

### Grouping Transactions
- The default view is **Grouped by Category** with collapsible sections
- **Tap a group header** to expand or collapse its transactions
- Each group shows:
  - ▶/▼ Expand/collapse indicator
  - Category name
  - Total amount for the category
  - Number of transactions
- Tap the grouping button (📄 List View / 📂 Grouped) to toggle between:
  - **Grouped View**: Transactions grouped by category with collapsible sections
  - **List View**: All transactions in chronological order

### Refreshing Data
- Pull down on the main page to refresh transaction data

## Database Schema

### Account Table
- Id (int, primary key, auto-increment)
- Name (string)
- InitialBalance (decimal)
- IsDefault (bool)

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
- AccountId (int?, foreign key, nullable - defaults to General account)

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
