---
name: create
description: Scaffold new pages, ViewModels, services, and repositories following project patterns
triggers:
  - "new page"
  - "create page"
  - "add page"
  - "new service"
  - "create service"
  - "new repository"
  - "add feature"
  - "scaffold"
---

# Create Skill — Scaffold New Components

## Before Creating

1. **CodeGraph first**: Query `codegraph_explore` to understand existing patterns for the component type you're creating. Examine a representative example:
   - Page: explore `AddTransactionPage` or `BudgetPage`
   - ViewModel: explore `AddTransactionViewModel` or `BudgetViewModel`
   - Repository: explore `TransactionRepository` or `AccountRepository`
   - Service: explore `BalanceService` or `TransactionEnrichmentService`

2. **Check for existing code** that can be reused before creating new files.

## Scaffolding Checklists

### New Page
1. Create `Views/XxxPage.xaml` with:
   - `ContentPage` root, `xmlns:ext` for localization, `x:DataType` for compiled bindings
   - `{ext:Localize Key}` for all user-visible text
   - `AppThemeBinding` for light/dark colors
   - `RefreshView` if the page has loadable data
   - Empty state with Material Design Icon when no data
2. Create `Views/XxxPage.xaml.cs` with:
   - Constructor: receive ViewModel via DI, set `BindingContext`
   - `OnAppearing()`: call `ViewModel.InitializeAsync()`
   - `OnBackButtonPressed()`: navigate to `//MainPage`
3. Register in `MauiProgram.cs`: `builder.Services.AddTransient<XxxPage>();`
4. Add route in `AppShell.xaml` (FlyoutItem) or `AppShell.xaml.cs` (`Routing.RegisterRoute`)
5. Add `<MauiXaml>` entry in `.csproj` if not auto-included

### New ViewModel
1. Create `ViewModels/XxxViewModel.cs`:
   - `public partial class XxxViewModel : ObservableObject`
   - Constructor with interface dependencies (injected via DI)
   - `[ObservableProperty]` for all bindable state
   - `[RelayCommand]` for all actions
   - `public async Task InitializeAsync()` for data loading
   - `partial void OnXxxChanged(T value)` for property change reactions
2. Register in `MauiProgram.cs`: `builder.Services.AddTransient<XxxViewModel>();`

### New Service
1. Create `Services/Interfaces/IXxxService.cs` with method signatures
2. Create `Services/XxxService.cs` implementing the interface
3. Register in `MauiProgram.cs`: `builder.Services.AddSingleton<IXxxService, XxxService>();`

### New Repository
1. Create `Services/Interfaces/IXxxRepository.cs` extending `IRepository<T>`
2. Create `Services/Repositories/XxxRepository.cs`:
   - `sealed class`, takes `DatabaseInitializer` in constructor
   - Call `_dbInitializer.Database!.Table<T>()` for queries
   - Call `EnsureInitializedAsync()` before every operation (helper method pattern from existing repos)
3. Add `CreateTableAsync<T>()` to `DatabaseInitializer.InitializeAsync()`
4. Register in `MauiProgram.cs`: `builder.Services.AddSingleton<IXxxRepository, XxxRepository>();`

### New Model
1. Create `Models/Xxx.cs` with `[PrimaryKey, AutoIncrement]` on `Id`
2. Use `[Ignore]` for computed/navigation properties
3. Add `CreateTableAsync<Xxx>()` to `DatabaseInitializer.InitializeAsync()`

### Localization
- Add keys to BOTH `Resources/Strings/AppResources.resx` (English) AND `AppResources.es.resx` (Spanish)
- Use descriptive PascalCase key names (e.g., `RecurringTransactionFrequency`)

## After Creating
Run the **verify** skill to confirm the build is clean.
