# Category Management Features

## Overview
Enhanced category management with full edit and delete capabilities including smart transaction reassignment.

## Features Implemented

### 1. **Rename/Edit Category**
- **How to use**: Swipe left on any category to reveal the "Edit" action
- **Behavior**: 
  - When edited, the category name updates across the entire system
  - All existing transactions automatically reflect the new category name (they remain linked by ID)
  - No data loss or balance changes occur
  - Edit mode shows a yellow-highlighted form with Cancel and Save buttons

### 2. **Delete Category Without Transactions**
- **Condition**: Category has no associated transactions
- **Behavior**:
  - Shows a simple confirmation dialog
  - If confirmed, category is deleted immediately
  - No impact on balances or transactions

### 3. **Delete Category With Transactions** (Smart Deletion)
- **Condition**: Category has existing transactions
- **Behavior**:
  - System detects that transactions exist
  - Shows an action sheet with all other categories of the same type
  - User must select a replacement category
  - All transactions are automatically reassigned to the selected category
  - Original category is then deleted
  - Success message confirms the operation
  - **Protection**: Cannot delete the last category if it has transactions

## User Interface

### Swipe Gestures
- **Swipe Left**: Edit category (blue background)
- **Swipe Right**: Delete category (red background)
- **Visual Hints**: "⬅️ Edit" and "Delete ➡️" labels on each category

### Edit Mode
- **Edit Form**: Yellow-highlighted frame appears when editing
- **Add Form**: Hides when in edit mode
- **Buttons**: Cancel (gray) and Save (green)
- **Entry Field**: Pre-filled with current category name

## Technical Implementation

### DatabaseService New Methods
```csharp
// Check if a category has transactions
Task<bool> CategoryHasTransactionsAsync(int categoryId)

// Reassign all transactions from one category to another
Task<int> ReassignTransactionsCategoryAsync(int fromCategoryId, int toCategoryId)
```

### ManageCategoriesViewModel New Properties
- `IsEditMode`: Controls visibility of edit/add forms
- `EditingCategory`: The category currently being edited
- `EditCategoryName`: Bound to the edit entry field

### ManageCategoriesViewModel New Commands
- `EditCategoryCommand`: Initiates edit mode for a category
- `SaveEditCommand`: Saves the edited category name
- `CancelEditCommand`: Cancels edit mode
- `DeleteCategoryCommand` (Enhanced): Smart deletion with transaction reassignment

## Business Logic Flow

### Edit Category Flow
1. User swipes left → taps "Edit"
2. Edit form appears with current name
3. User modifies name → taps "Save"
4. Category updates in database
5. All views refresh
6. Edit mode closes

### Delete Category Flow

#### Without Transactions
1. User swipes right → taps "Delete"
2. Confirmation dialog appears
3. User confirms
4. Category deleted
5. List refreshes

#### With Transactions
1. User swipes right → taps "Delete"
2. System checks for transactions
3. Action sheet shows replacement categories
4. User selects replacement category
5. All transactions reassigned
6. Original category deleted
7. Success message shown
8. List refreshes

## Error Handling
- **Empty name**: Shows error dialog
- **Last category with transactions**: Cannot delete, shows informative message
- **Cancel during edit**: Reverts changes
- **Cancel during delete**: No action taken

## UI/UX Features
- Clear visual feedback (color-coded)
- Intuitive swipe gestures
- Confirmation dialogs prevent accidental deletion
- Edit mode prevents adding new categories simultaneously
- Helpful hints on each category item
- Color scheme:
  - Blue: Edit
  - Yellow: Edit mode active
  - Red: Delete
  - Green: Save
  - Gray: Cancel

## Data Integrity
✅ All transaction-category relationships maintained
✅ No orphaned transactions
✅ Balance calculations remain accurate
✅ Category IDs preserve relationships
✅ Transaction history intact
