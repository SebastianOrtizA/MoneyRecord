# Stage 17: Dex / R8 optimization (Play Console warning)

**Status**: 17 done (PR #85 + workflow fix); the Play warning clears after the next upload. Warning: "La optimización de código DEX está por debajo de nuestro umbral — Ofuscación (1 %)"
**Depends on**: CI Play upload (PR #70)
**Complexity**: Small to medium (research, then one PR)
**Source**: User request, 2026-10-25; the trigger is a Play Console warning

## Goal
Clear the Play Console warning and make a measured decision on R8 for the Android Release build.

## Current state
`MoneyRecord.csproj` sets no `AndroidLinkTool`, so the Release build runs D8 only: no R8 shrinking or minifying and no
`mapping.txt`. Trimming uses the .NET defaults. CI publishes the AAB with `dotnet publish`
(`.github/workflows/build-android.yml`) and uploads it to closed testing.

## Sub-stages

### 17.1 Research (deliverable: a findings section in this file, and a PR comment)
- Get the **exact** warning text from Play Console (the owner pastes it). It is most likely "no deobfuscation file", an R8
  recommendation, or a dex size warning.
- Measure the baseline against `<AndroidLinkTool>r8</AndroidLinkTool>` in Release: AAB / universal APK size (`bundletool`), dex
  count and size, cold start (`am start -W`, five runs), build time.
- Work out the keep rules the Java libraries need when reflected or loaded by name: ML Kit text recognition, AndroidX
  WorkManager (`DailyReminderWorker`), the widget provider and configure activity, Biometric, SAF. Write them as
  `Platforms/Android/proguard.cfg` (`<ProguardConfiguration>`).
- Note, without enabling them: `AndroidEnableProfiledAot` (default) and startup tracing.

### 17.2 Apply (only if 17.1 shows a gain or the warning requires it)
- Release-only csproj properties + `proguard.cfg`. CI passes the generated `mapping.txt` to the Play upload step
  (`mappingFile`) so the warning clears, and keeps it as a build artifact.
- Smoke-test the Release build on the emulator: start, add an expense, the widget, the daily worker
  (`adb shell cmd jobscheduler run`), receipt OCR, the backup folder picker, the app lock. R8 problems only show up at runtime.

## Critical files
- `MoneyRecord/MoneyRecord.csproj`, `MoneyRecord/Platforms/Android/proguard.cfg` (new), `.github/workflows/build-android.yml`,
  `CLAUDE.md` (build notes)

## Verify
- The numbers table in this file; a Release AAB built in CI; the Play Console upload shows no warning; the smoke test passes.

## Findings (2026-10-25)
- Play warning: "DEX code optimization is below our threshold: Obfuscation (1 %)". Play's "App optimization" switch in Advanced
  settings does not change it (it repackages the APKs, it doesn't rename our code).
- The SDK's `proguard_xamarin.cfg` (used with `AndroidLinkTool=r8`) starts with `-dontobfuscate`, so R8 alone renames nothing.
  The csproj target `DropSdkDontObfuscate` removes it from R8's input; its keep rules (without that line) and `-printmapping`
  moved to `Platforms/Android/proguard.cfg` and a generated `proguard_mapping.cfg`.
- Runtime failures found on the emulator and fixed with keep rules: `net.dot.android.ApplicationRegistration` (ClassNotFound),
  `Lifecycle$State.DESTROYED` (members of the androidx/Google/Kotlin libraries are read over JNI: `-keepclassmembers`), and
  `FitWindowsFrameLayout` (views inflated from XML by name: keep View/Fragment/Activity/etc. constructors).
- Numbers (local Release publish): APK 74.4 MB → 71.4 MB, mapping.txt 32 MB, build ~165 s both, classes renamed 76 % (11,617
  of 15,317 in `mapping.txt`); 15 % with a blanket `-keep class androidx.**`, hence `-keepclassmembers`).
- Smoke test (Release APK, x86_64 emulator): start, add expense, reports chart, Settings/More, WorkManager job runs, receipt
  OCR (ML Kit) fills amount and date. Not run: widget, biometric lock, backup folder picker, notifications.

## Follow-up (2026-10-28): Play still below 25 %
- v0.19.2 on Play: Optimization 0 %, Obfuscation 18 %, Shrinking 18 % (R8 full mode on). The blanket `-keepclassmembers` over
  `androidx.**`, `com.google.**`, `kotlin.**` and `kotlinx.**` keeps every member of those libraries: no renaming, shrinking or
  inlining of them.
- Narrowed to the packages the bindings read by name over JNI (androidx core/appcompat/lifecycle/work/security/..., Material,
  ML Kit, Kotlin). Not kept any more: Sentry, Glide, GMS, Firebase, Gson, crypto and the other `com.google.**` libraries.
- Each missing rule is a runtime crash, found by running the code: `Insets.left` (androidx.core, on start) and
  `MasterKey$KeyScheme.AES256_GCM` (androidx.security, Settings: SecureStorage).
- Numbers (local Release): mapping.txt 32.7 MB → 30.9 MB, APK 71.4 → 71.8 MB, about 99 % of the mapped members renamed.
- Smoke test (Release APK): start, add expense, reports chart, Settings, daily worker + bill notification, receipt OCR, widget,
  biometric lock, backup folder picker, backup password (set, protected restore, wrong password), database encryption.
- Open: whether Play's numbers reach 25 % is only known after uploading a build; optimization may stay near 0 %.
