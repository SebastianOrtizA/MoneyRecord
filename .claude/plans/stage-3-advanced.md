# Stage 3: Advanced Features

**Status**: Complete
**Depends on**: Stages 0 and 2 complete
**Complexity**: Medium to High

## Goal
Power-user features and quality-of-life additions for a more complete finance app.

## Tasks

### 3.1 Enhanced Dashboard
- Modify `Views/MainPage.xaml` — redesign header with summary cards:
  - Total balance card (prominent)
  - Income/expense mini cards with period comparison
  - Budget status compact view
  - Recent transactions quick list
- Modify `ViewModels/MainViewModel.cs` — add summary card data properties

### 3.2 Tags on Transactions
- Modify `Models/Transaction.cs` — add `string Tags` field (comma-separated, stored in SQLite)
- SQLite handles new columns automatically via `CreateTableAsync` (no migration needed)
- Modify `Views/AddTransactionPage.xaml` — add tags input field
- Modify `ViewModels/AddTransactionViewModel.cs` — handle tags
- Integrate with search (Stage 2.3) — filter by tags

### 3.3 Multi-Currency Support (v1 — display only)
- Create `Models/Currency.cs` — Code, Symbol, Name
- Create `Services/Interfaces/ICurrencyService.cs` and `Services/CurrencyService.cs`
- Modify `Models/Account.cs` — add `CurrencyCode` field (default to device locale currency)
- Display correct currency symbol per account
- Warn when transferring between different-currency accounts
- No live exchange rates in v1 (manual rates if needed)

### 3.4 Receipt Photos
- Modify `Models/Transaction.cs` — add `string? ReceiptPhotoPath` field
- Modify `Views/AddTransactionPage.xaml` — photo capture/pick button + preview thumbnail
- Modify `ViewModels/AddTransactionViewModel.cs` — use MAUI `MediaPicker` for camera/gallery
- Store photos in `FileSystem.AppDataDirectory/receipts/`
- Display photo in transaction detail view

## Dependencies
- 3.1 builds on MainPage structure (independent of other Stage 3 tasks)
- 3.2 integrates best after Stage 2.3 (search) for tag filtering
- 3.3 is the most complex — touches core financial logic
- 3.4 is independent
