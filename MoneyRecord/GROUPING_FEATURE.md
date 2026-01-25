# Transaction Grouping by Category Feature

## Overview
Added the ability to group transactions by category on the main screen, showing all transactions for each category together with category totals.

## Features Implemented

### 1. **TransactionGroup Model**
Created `Models/TransactionGroup.cs`:
- Represents a group of transactions for a specific category
- Properties:
  - `CategoryName`: Name of the category
  - `Total`: Sum of all transactions in the group
  - `TransactionCount`: Number of transactions in the group
  - `Type`: Type of transactions (Income/Expense)
- Extends `ObservableCollection<Transaction>` for MAUI data binding

### 2. **MainViewModel Updates**
Added grouping logic to `MainViewModel.cs`:
- **New Properties:**
  - `GroupedTransactions`: Observable collection of transaction groups
  - Updated `IsGroupedByCategory`: Controls whether transactions are grouped
  
- **New Command:**
  - `ToggleGroupingCommand`: Toggles between grouped and list view

- **Updated LoadDataAsync():**
  - When `IsGroupedByCategory = true`: Groups transactions by category name
  - When `IsGroupedByCategory = false`: Shows flat list of transactions
  - Automatically clears the unused collection

### 3. **UI Updates (MainPage.xaml)**
Added two separate CollectionViews:

#### **Ungrouped View** (List View)
- Visible when `IsGroupedByCategory = false`
- Shows transactions in a flat list
- Displays: Date, Amount, Description, Category Name
- Same layout as before

#### **Grouped View** 
- Visible when `IsGroupedByCategory = true`
- Uses MAUI's `IsGrouped="True"` feature
- **Group Header** shows:
  - Category Name (large, bold)
  - Transaction count (e.g., "5 items")
  - Category Total (color-coded by type)
  - Styled with blue theme
- **Group Items** show:
  - Date
  - Amount (color-coded)
  - Description
  - Indented slightly for visual hierarchy

#### **Toggle Button**
- Orange button in the middle of the control bar
- Text changes: "📄 List View" ↔ "📂 Grouped"
- Click to toggle between views

### 4. **New Converter**
Added `BoolToGroupTextConverter`:
- Converts boolean to display text
- `true` → "📂 Grouped"
- `false` → "📄 List View"

## How It Works

### User Experience

1. **Open Main Page** → Transactions shown in list view by default
2. **Click "📄 List View" button** → View changes to grouped
3. **Grouped View displays:**
   ```
   ┌─────────────────────────────────────┐
   │ Food              5 items    $145.50│
   ├─────────────────────────────────────┤
   │   Jan 15, 2024         $25.00       │
   │   Description here                  │
   ├─────────────────────────────────────┤
   │   Jan 14, 2024         $50.00       │
   │   Another transaction               │
   └─────────────────────────────────────┘
   
   ┌─────────────────────────────────────┐
   │ Transportation    3 items    $67.00 │
   ├─────────────────────────────────────┤
   │   Jan 13, 2024         $30.00       │
   │   Gas station                       │
   └─────────────────────────────────────┘
   ```

4. **Click "📂 Grouped" button** → Returns to list view
5. **Grouping persists** when changing periods or date ranges

### Technical Flow

```csharp
// When toggling grouping
ToggleGroupingCommand → IsGroupedByCategory = !IsGroupedByCategory → LoadDataAsync()

// In LoadDataAsync()
if (IsGroupedByCategory)
{
    // Group by category
    var groups = transactions
        .GroupBy(t => t.CategoryName)
        .Select(g => new TransactionGroup(g.Key, g.ToList()))
        .OrderBy(g => g.CategoryName);
    
    GroupedTransactions.Clear();
    foreach (var group in groups)
        GroupedTransactions.Add(group);
    
    Transactions.Clear(); // Hide ungrouped view
}
else
{
    // Show flat list
    Transactions.Clear();
    foreach (var transaction in transactions)
        Transactions.Add(transaction);
    
    GroupedTransactions.Clear(); // Hide grouped view
}
```

### XAML Binding

```xml
<!-- Grouped CollectionView -->
<CollectionView ItemsSource="{Binding GroupedTransactions}"
               IsVisible="{Binding IsGroupedByCategory}"
               IsGrouped="True">
    <CollectionView.GroupHeaderTemplate>
        <!-- Shows CategoryName, Count, Total -->
    </CollectionView.GroupHeaderTemplate>
    <CollectionView.ItemTemplate>
        <!-- Shows individual transactions -->
    </CollectionView.ItemTemplate>
</CollectionView>
```

## Benefits

### For Users
✅ **Better Organization**: See all transactions for a category together
✅ **Quick Totals**: Instantly see spending/income per category
✅ **Easy Comparison**: Compare categories at a glance
✅ **Flexible Views**: Toggle between grouped and list views
✅ **Transaction Counts**: Know how many transactions per category

### For Analysis
✅ **Category Spending**: Identify highest spending categories
✅ **Income Sources**: See which income categories contribute most
✅ **Budgeting**: Track category totals against budgets
✅ **Patterns**: Spot spending patterns by category

## Color Coding

- **Income Categories**: Green totals
- **Expense Categories**: Red totals
- **Group Headers**: Blue theme (light/dark mode)
- **Individual Transactions**: Standard color scheme

## Styling

### Group Headers
- Background: Light blue (#E3F2FD in light mode, #1E3A5F in dark mode)
- Border: Blue accent
- Large category name (18pt, bold)
- Small transaction count (12pt, gray)
- Large total amount (20pt, bold, color-coded)

### Individual Items (in Groups)
- Slightly indented (left margin: 15)
- Date and amount on same line
- Description below
- Compact layout

## Example Use Cases

### 1. Monthly Budget Review
- Group by category
- See total spent per category
- Compare against budget limits
- Identify overspending

### 2. Income Analysis
- Filter to income only
- Group by category
- See which income sources are most significant
- Plan for irregular income

### 3. Expense Tracking
- Filter to expenses
- Group by category
- Identify unnecessary spending categories
- Make budgeting decisions

### 4. Period Comparison
- Select custom period
- Group transactions
- Export data mentally for comparison
- Track trends over time

## Future Enhancements (Optional)

- Add sorting options for groups (by name, total, count)
- Add expand/collapse for individual groups
- Add category icons
- Add percentage of total for each category
- Add visual charts/graphs per category
- Add budget limits per category with progress bars
- Add filters (show only income categories, only expense categories)
- Add search within groups
- Add ability to edit transactions from grouped view

## Technical Notes

- Uses MAUI's native grouping support
- No third-party libraries required
- Efficient LINQ grouping
- Responsive to light/dark mode
- Works on all platforms (Android, iOS, Windows, macOS)
