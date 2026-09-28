---
name: verify
description: Build the project and verify changes compile cleanly, check DI registrations and XAML bindings
triggers:
  - "verify build"
  - "check build"
  - "does it compile"
  - "build errors"
  - "after changes"
---

# Verify Skill — Build & Integration Check

## Steps

1. **Build Android target** (primary):
   ```bash
   dotnet build MoneyRecord/MoneyRecord.csproj -f net10.0-android
   ```

2. **Parse errors**: If the build fails, categorize errors:
   - **Missing using directives**: Add the correct namespace import
   - **Unregistered DI services**: Check `MauiProgram.cs` for missing `builder.Services.AddSingleton/AddTransient` registrations
   - **XAML binding errors**: Check `x:DataType` matches the ViewModel, verify property names match `[ObservableProperty]` generated names (field `myProp` → property `MyProp`)
   - **Missing Shell routes**: Check `AppShell.xaml` and `AppShell.xaml.cs` for route registrations
   - **Missing localization keys**: Check both `AppResources.resx` and `AppResources.es.resx`

3. **CodeGraph blast radius check**: After modifying a service or interface, use `codegraph_explore` to verify all callers are updated. Pay special attention to:
   - Interface changes — all implementations must match
   - Constructor parameter changes — DI will fail at runtime if not registered
   - Model property additions — SQLite `CreateTableAsync` auto-adds columns but views/converters may need updates

4. **Quick smoke checks**:
   - Every new page must be registered in `MauiProgram.cs` (Transient) and have a Shell route
   - Every new ViewModel must be registered in `MauiProgram.cs` (Transient, except MainViewModel = Singleton)
   - Every new service/repository must be registered as Singleton with its interface
   - New localization keys must exist in BOTH .resx files (English and Spanish)
