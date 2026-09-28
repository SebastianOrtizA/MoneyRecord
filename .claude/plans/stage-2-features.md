# Stage 2: New Essential Features

**Status**: Complete
**Depends on**: Stage 0 complete (Stage 1 recommended but not required)
**Complexity**: High

## Goal
Add the most-requested personal finance features: recurring transactions, backup/restore, search, and charts.

## Tasks

### 2.1 Recurring Transactions — DONE

**Model**: ✅
- Created `Models/RecurringTransaction.cs` — entity + `RecurrenceFrequency` enum + `RecurrenceFrequencyItem` picker model
- Properties: Id, Amount, Description, CategoryId, AccountId, TransactionType, Frequency, StartDate, EndDate (nullable), NextOccurrence, LastProcessedDate, IsActive, DisplayIcon

**Data layer**: ✅
- Modified `Services/Repositories/DatabaseInitializer.cs` — `CreateTableAsync<RecurringTransaction>()`
- Created `Services/Interfaces/IRecurringTransactionRepository.cs` — GetActiveAsync, GetDueAsync, DeactivateAsync, DeleteByIdAsync
- Created `Services/Repositories/RecurringTransactionRepository.cs`

**Processing service**: ✅
- Created `Services/Interfaces/IRecurringTransactionService.cs`
- Created `Services/RecurringTransactionService.cs`
- On startup: processes all due recurring transactions, creates actual transactions for each past-due NextOccurrence, handles gaps (while loop catches up)

**UI**: ✅
- Modified `Views/AddTransactionPage.xaml` — "Make Recurring" toggle + frequency picker + optional end date
- Modified `ViewModels/AddTransactionViewModel.cs` — IsRecurring, SelectedFrequencyItem, HasEndDate, EndDate; saves RecurringTransaction on new transaction creation
- Created `Views/RecurringTransactionsPage.xaml` + `.cs` — management page with cards showing description, category, amount, frequency, next occurrence
- Created `ViewModels/RecurringTransactionsViewModel.cs` — load, delete, toggle active

**Integration**: ✅
- Modified `AppShell.xaml` — added "Recurring" flyout entry
- Modified `MauiProgram.cs` — registered IRecurringTransactionRepository, IRecurringTransactionService, RecurringTransactionsViewModel, RecurringTransactionsPage
- Modified `App.xaml.cs` — triggers ProcessDueTransactionsAsync on window Created event
- Added 18 localization strings to both .resx files + Designer.cs

### 2.2 Data Backup & Restore — DONE

**Service**: ✅
- Created `Services/Interfaces/IDataExportService.cs`
- Created `Services/DataExportService.cs`
- Full backup: JSON export of all 6 tables (Category, Account, Transaction, Transfer, Budget, RecurringTransaction)
- CSV export: transactions only with enriched category/account names
- Import: JSON deserialize, delete all, re-insert with ID remapping for foreign keys

**UI**: ✅
- Created `Views/SettingsPage.xaml` + `.cs`
- Created `ViewModels/SettingsViewModel.cs`
- Three buttons: Export Full Backup (JSON), Export Transactions (CSV), Restore from Backup
- Uses Share API for export, FilePicker for import, confirmation dialog before restore

**Integration**: ✅
- Modified `AppShell.xaml` — added Settings flyout entry
- Modified `MauiProgram.cs` — registered IDataExportService, SettingsViewModel, SettingsPage
- Added 19 localization strings to both .resx files + Designer.cs

### 2.3 Transaction Search & Filters — DONE

**Search**: ✅
- Modified `Views/MainPage.xaml` — SearchBar with placeholder, bound to SearchText
- Modified `ViewModels/MainViewModel.cs` — SearchText with real-time filtering on description, category, account

**Advanced Filters**: ✅
- Modified `ViewModels/MainViewModel.cs` — filter by type (All/Income/Expense/Transfer), category picker, account picker, amount range (min/max)
- Modified `Views/MainPage.xaml` — collapsible filter panel with purple accent, filter toggle button, active filter count indicator, clear button
- Client-side filtering via cached `_allCombinedTransactions` (no re-fetch on filter change)
- Added 11 localization strings to both .resx files + Designer.cs

### 2.4 Improved Reports with Charts — DONE

**Charting library**: ✅
- Modified `MoneyRecord.csproj` — added LiveChartsCore.SkiaSharpView.Maui 2.0.5

**Trends page**: ✅
- Created `Views/TrendsReportPage.xaml` + `.cs`
- Created `ViewModels/TrendsReportViewModel.cs`
- Monthly spending bar chart (coral bars, configurable 3/6/12 months)
- Income vs expenses comparison (blue vs red grouped bars)
- Category breakdown pie chart (top 8 expense categories with color coding)

**Integration**: ✅
- Modified `AppShell.xaml` — added Trends flyout entry (chart icon, after IncomeReport)
- Modified `MauiProgram.cs` — registered TrendsReportViewModel, TrendsReportPage, `.UseLiveCharts()`
- Added 7 localization strings to both .resx files + Designer.cs

## Internal Dependencies
- 2.1 through 2.4 are independent of each other
- 2.2 (backup) should include RecurringTransaction table if 2.1 is done first
