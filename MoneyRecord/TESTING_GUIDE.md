# Testing Guide: Category Deletion with Transactions

## Test Scenario: Delete Category with Transactions

This guide will help you verify that the category deletion feature works correctly when a category has associated transactions.

## Prerequisites
1. Application must be running
2. Database must be initialized with default categories

## Test Steps

### Test 1: Delete Category WITHOUT Transactions

**Steps:**
1. Open the app
2. Navigate to **Manage Categories** → **Income Categories** (or Expense Categories)
3. Create a new category (e.g., "Test Category")
4. Click **Delete** button on the newly created category
5. **Expected Result:** Simple confirmation dialog appears asking "Are you sure you want to delete 'Test Category'?"
6. Click **Yes**
7. **Expected Result:** Category is deleted immediately

### Test 2: Delete Category WITH Transactions (Multiple Categories Available)

**Steps:**
1. Open the app
2. Click **Add Income** or **Add Expense**
3. Create a transaction with a specific category (e.g., "Salary" category)
   - Select Date: Today
   - Enter Amount: 1000
   - Select Category: "Salary"
   - Click **Save**
4. Navigate to **Manage Categories** → **Income Categories**
5. Click **Delete** button on "Salary" category
6. **Expected Result:** 
   - An action sheet appears with the message: "'Salary' has existing transactions. Select a category to move them to:"
   - List shows other income categories (Freelance, Investment, Other Income)
   - Options include: Cancel button
7. Select a replacement category (e.g., "Freelance")
8. **Expected Result:**
   - Success alert appears: "Category 'Salary' deleted and 1 transaction(s) moved to 'Freelance'"
   - Category list refreshes
   - "Salary" category is removed
   - "Freelance" category now has the transaction
9. Verify on Main Page that the transaction now shows "Freelance" category

### Test 3: Delete LAST Category WITH Transactions

**Steps:**
1. Open the app
2. Navigate to **Manage Categories** → **Income Categories**
3. Delete all income categories EXCEPT ONE (e.g., keep only "Salary")
4. Ensure "Salary" category has at least one transaction (create one if needed)
5. Click **Delete** button on "Salary" category
6. **Expected Result:**
   - Alert appears: "This is the last category of this type. You cannot delete it while it has transactions. Please create another category first."
   - Category is NOT deleted
7. Create a new category (e.g., "New Income")
8. Try deleting "Salary" again
9. **Expected Result:**
   - Action sheet appears asking to select "New Income" as replacement
   - After selecting, transactions are moved and category is deleted

## Debugging

If the feature is not working as expected, check the **Output** window in Visual Studio for debug messages:

### Expected Debug Messages:

**When deleting a category with transactions:**
```
Attempting to delete category: Salary (ID: 1)
Category 1 has 5 transactions
Available categories for reassignment: 3
User selected: Freelance
Reassigning transactions from 1 to 2
Reassigned 5 transactions
```

**When deleting a category without transactions:**
```
Attempting to delete category: Test Category (ID: 10)
Category 10 has 0 transactions
Category Test Category deleted (no transactions)
```

## Common Issues

### Issue 1: Action sheet doesn't appear
**Possible Cause:** Transactions might not be properly linked to categories
**Solution:** 
1. Check Output window for "Category X has 0 transactions" message
2. Verify transactions have correct CategoryId in database
3. Create a new transaction and verify the CategoryId is set correctly

### Issue 2: Transactions not reassigned
**Possible Cause:** ReassignTransactionsCategoryAsync might be failing
**Solution:**
1. Check Output window for error messages
2. Verify both fromCategoryId and toCategoryId exist in database

### Issue 3: Simple deletion dialog appears instead of action sheet
**Possible Cause:** CategoryHasTransactionsAsync returns false
**Solution:**
1. Check Output window for "Category X has 0 transactions"
2. Verify transaction was created with correct CategoryId
3. Check that transaction Date is within query range

## Verification Checklist

- [ ] Can delete category without transactions (shows simple confirmation)
- [ ] Can delete category with transactions (shows action sheet with replacement options)
- [ ] Transactions are successfully reassigned to selected category
- [ ] Original category is deleted after reassignment
- [ ] Success message shows correct count of reassigned transactions
- [ ] Cannot delete last category if it has transactions
- [ ] Category list refreshes after deletion
- [ ] Main page shows updated category names for transactions
- [ ] Balance remains accurate after category deletion

## Technical Details

### Key Methods:

1. **CategoryHasTransactionsAsync(int categoryId)**
   - Returns true if category has any transactions
   - Adds debug logging

2. **ReassignTransactionsCategoryAsync(int fromCategoryId, int toCategoryId)**
   - Moves all transactions from one category to another
   - Returns count of reassigned transactions

3. **DeleteCategoryAsync(Category category)**
   - In ViewModel, handles the entire deletion flow
   - Checks for transactions
   - Shows appropriate UI based on transaction existence
   - Manages transaction reassignment

### Database Queries:

```csharp
// Check transactions
_database.Table<Transaction>()
    .Where(t => t.CategoryId == categoryId)
    .CountAsync();

// Reassign transactions
var transactions = _database.Table<Transaction>()
    .Where(t => t.CategoryId == fromCategoryId)
    .ToListAsync();

foreach (var transaction in transactions)
{
    transaction.CategoryId = toCategoryId;
    await _database.UpdateAsync(transaction);
}
```

## Support

If tests fail, check:
1. Visual Studio Output window for debug messages
2. Ensure database is properly initialized
3. Verify all code changes were saved and built
4. Try clean rebuild of the project
