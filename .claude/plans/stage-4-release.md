# Stage 4: Release Preparation

**Status**: Pending
**Depends on**: Stages 0-2 complete, Stage 3 features included incrementally
**Complexity**: High (infrastructure setup)

## Goal
Testing, CI/CD, and Android APK store readiness.

## Tasks

### 4.1 Unit Tests

**Test project setup**:
- Create `MoneyRecord.Tests/MoneyRecord.Tests.csproj` (xUnit)
- Add NSubstitute for mocking, FluentAssertions for readable assertions
- Reference `MoneyRecord.csproj`

**Priority test targets** (ordered by criticality):
1. `BalanceService` — all balance calculation methods (most critical business logic)
2. `BudgetProjectionHelper` — pure static methods, complex period math
3. `DateRangeHelper` — pure static methods, date edge cases
4. `TransactionEnrichmentService` — enrichment/join logic
5. Validators (from Stage 1.4) — input validation rules
6. `CurrencyMaskBehavior.ParseCurrencyValue` — currency parsing
7. ViewModels — command behavior with mocked repositories

**Testing strategy**:
- Mock repository interfaces (IAccountRepository, etc.) with NSubstitute
- Test business services with mocked repos (no SQLite in unit tests)
- Test ViewModels by verifying observable property changes and command behavior

### 4.2 CI/CD Pipeline

**Create**: `.github/workflows/build-android.yml`

```yaml
Workflow:
1. Trigger: push to main / pull request
2. Setup .NET 10 SDK
3. Install MAUI workloads
4. Build Android APK (dotnet build -c Release -f net10.0-android)
5. Run unit tests
6. Upload APK as GitHub artifact
7. (Optional) Create GitHub Release with APK on tag push
```

### 4.3 APK Signing & Store Preparation

- Modify `MoneyRecord.csproj` — proper `ApplicationId`, signing config
- Modify `Platforms/Android/AndroidManifest.xml` — review permissions, metadata
- Create Android keystore for APK signing
- Prepare store listing assets (screenshots, descriptions in en/es)
- Keep all platform targets in .csproj (user decision)

### 4.4 Performance Profiling

- Measure startup time — optimize `InitializeAsync` chains
- Memory usage — check for leaks in singleton MainPage/MainViewModel
- SQLite query performance under load (test with 1000+ transactions)
- Android profiler via Visual Studio or `dotnet trace`
