# Stage 9: Release Hardening

**Status**: In progress (9.1 done; 9.2 in review)
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
- Done as: the `build-android` job decodes the secret when it is set (any build) and fails on `v*` tags when it
  isn't (any of the three secrets missing → `::error::` naming them), so the check runs before the build instead of in the `release` job. Each secret is scoped to the step that uses it (the decode step only sees whether the passwords are set). Release builds already produce both
  formats (`*-Signed.apk` + `*-Signed.aab`), so `-p:AndroidPackageFormat=apk` was dropped; both are uploaded as one
  `MoneyRecord-Android` artifact and attached to the release. The decoded keystore is deleted after the build.
  Key creation + `gh secret set` steps are in the README ("Releases"). Checked locally with a throwaway keystore:
  `apksigner` shows its certificate on the APK, `keytool -printcert` on the AAB, and `-p:ApplicationVersion` /
  `-p:ApplicationDisplayVersion` overrides reach the manifest (input for 9.2). The secrets themselves are created by
  the owner; the `v0.9.0-rc1` verify waits for them.

### 9.2 Versioning from tags
- `ApplicationDisplayVersion` from the tag (`v1.2.0` → `1.2.0`), `ApplicationVersion` from `github.run_number`;
  pass with `-p:` in the workflow.
- Done as: a `Set version` step builds the `-p:` arguments. Every CI build gets `ApplicationVersion` = run number;
  only `v*` tags set `ApplicationDisplayVersion`, and a tag that isn't `v<major>.<minor>.<patch>[-suffix]` fails the
  build. Deviation: a suffix is kept (`v0.9.0-rc1` → versionName `0.9.0-rc1`, which About shows) instead of being
  stripped to `0.9.0`, and such a tag makes the GitHub Release a pre-release. README "Releases" explains the scheme.
  Checked locally: `-p:ApplicationDisplayVersion=0.9.0-rc1 -p:ApplicationVersion=7` builds and `aapt2 dump badging`
  shows versionCode 7 / versionName 0.9.0-rc1. Also: an invalid `ANDROID_KEYSTORE_B64` now fails with an `::error::`
  instead of a .NET exception (the first secret set by hand was not valid base64), and the README pipes the base64
  into `gh secret set` (with `$PWD`, since .NET resolves relative paths against the process directory).

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
