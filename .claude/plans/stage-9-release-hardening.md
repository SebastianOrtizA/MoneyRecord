# Stage 9: Release Hardening

**Status**: Pending
**Depends on**: Stage 6 (can run in parallel with Stages 7–8)
**Complexity**: Medium
**Source**: Product & engineering review, 2026-10-11 (Tests & CI, P11) + parts of Stage 5.1

## Goal
Public releases users can install and update, with enough logging to debug field problems.

## Tasks

### 9.1 Signed releases from CI
- Store the keystore as a base64 GitHub secret (`ANDROID_KEYSTORE_B64`) plus `KEYSTORE_PASSWORD`, `KEY_PASSWORD`.
- Workflow step decodes it to `MoneyRecord/moneyrecord.keystore` before `dotnet publish` (the csproj already
  signs when the file exists).
- Fail the `release` job (tag builds) if the keystore is missing, so unsigned APKs are never published.
- Publish both `.apk` (GitHub Releases / F-Droid) and `.aab` (Play Store).

### 9.2 Versioning from tags
- `ApplicationDisplayVersion` from the tag (`v1.2.0` → `1.2.0`), `ApplicationVersion` from `github.run_number`;
  pass with `-p:` in the workflow.

### 9.3 Permissions and privacy
- Remove unused `INTERNET`, `ACCESS_NETWORK_STATE`, `READ/WRITE_EXTERNAL_STORAGE` from `AndroidManifest.xml`
  (MediaPicker/FilePicker don't need them). Re-add `INTERNET` only with Stage 10 FX rates / cloud backup, behind opt-in.
- `android:allowBackup`: keep, but add `dataExtractionRules` so Android backup includes the DB and receipts and
  excludes `files/.__override__` and caches.
- Privacy section in README: offline, no tracking.

### 9.4 Logging and crash reports
- Serilog (Apache-2.0) + `Serilog.Sinks.File` to `AppDataDirectory/logs`, 7-day rolling; `ErrorHandler` logs there.
- Settings → "Share logs" (zip via Share API).
- Opt-in crash reporting (Sentry .NET SDK, MIT, or self-hosted GlitchTip), default **off**, asked once after
  first successful week. (Replaces Stage 5.1.)

### 9.5 Tests and CI
- CI test job on `ubuntu-latest` once Stage 7.1 lands (Core-only tests).
- Add tests: `RecurringTransactionService` (catch-up of missed periods, end date), backup/restore round-trip,
  delete cascades, migrations.
- Coverage report (coverlet, MIT) uploaded as an artifact; no hard threshold yet.

### 9.6 Store and F-Droid readiness
- Fastlane metadata folder (`fastlane/metadata/android/{en-US,es-ES}`) with descriptions + screenshots taken via
  `/run-moneyrecord`.
- Check F-Droid inclusion rules (no proprietary deps, reproducible build notes).
- `THIRD-PARTY-NOTICES.md` listing all dependency licences (MDI font Apache-2.0, CommunityToolkit MIT, …).

## Verify
- Tag `v0.9.0-rc1` on a test branch → workflow produces signed APK + AAB with version `0.9.0`.
- Install the APK over a previous signed build on the emulator: the update keeps data.
- `/run-moneyrecord`: app works with the reduced permission set (camera, gallery pick, backup share).

## Done when
Tag builds produce signed, versioned APK/AAB; unused permissions are gone; logs can be shared from Settings.
