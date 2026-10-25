# Stage 17: Dex / R8 optimization (Play Console warning)

**Status**: Design proposed 2026-10-25; not started (needs the exact Play Console warning text first)
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
