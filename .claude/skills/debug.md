---
name: debug
description: Diagnose runtime issues, crashes, and unexpected behavior using CodeGraph-first tracing
triggers:
  - "crash"
  - "exception"
  - "bug"
  - "not working"
  - "error at runtime"
  - "doesn't load"
  - "blank page"
  - "null reference"
---

# Debug Skill — Runtime Issue Diagnosis

## Steps

1. **CodeGraph first**: Query `codegraph_explore` with the symbol or area where the issue occurs. Trace:
   - Call paths from the entry point (page `OnAppearing` → ViewModel `InitializeAsync` → service → repository)
   - Blast radius — what else depends on the broken symbol
   - Dynamic dispatch: DI-injected interfaces → concrete implementations

2. **Common MoneyRecord failure points**:

   **DI Resolution Failures** (app crashes on navigation):
   - Check `MauiProgram.cs` — is the service/page/VM registered?
   - Check constructor parameters — do they match registered interfaces?
   - Singleton vs Transient conflicts: a Transient VM depending on a Transient service is fine, but watch for captured state in Singletons

   **Database Failures** (data not loading):
   - Is the table created in `DatabaseInitializer.InitializeAsync()`?
   - Is `EnsureInitializedAsync()` called before the operation in the repository?
   - Check `SemaphoreSlim` deadlocks — avoid calling `InitializeAsync` from within itself

   **Navigation Failures** (page not found):
   - Verify route is registered in `AppShell.xaml` (FlyoutItem) or `AppShell.xaml.cs` (`Routing.RegisterRoute`)
   - Check route string matches exactly (case-sensitive): `nameof(PageClass)` vs literal string

   **XAML Binding Failures** (data not showing):
   - Check `BindingContext` is set in page constructor
   - Check `x:DataType` points to correct ViewModel
   - Check `[ObservableProperty]` naming: field `myProp` generates property `MyProp`
   - Check `OnPropertyChanged` is called (source generators do this, but manual properties need it)
   - Check `MainThread.InvokeOnMainThreadAsync` wraps UI updates from async code

   **Localization Failures** (keys showing instead of text):
   - Check key exists in `AppResources.resx`
   - Check `{ext:Localize KeyName}` syntax in XAML
   - Check `LocalizationService.Instance` is initialized before XAML loads

3. **Android-specific debugging**:
   - ADB logcat: `adb logcat -s mono-rt:* MonoAndroid:* dotnet:*`
   - Check `AndroidManifest.xml` for missing permissions
   - Check `MainActivity.cs` `ConfigChanges` flags for orientation/resize crashes

4. **Reproduce and fix**: After identifying the root cause, fix the issue and run the verify skill to confirm the build is clean.
