# Stage 9: Release Hardening

**Status**: In progress (9.1–9.5 done; 9.6 in review)
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
  the owner (done 2026-10-14; see the 9.2 verification).

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
- Verified with the real secrets (2026-10-14). PR #30's CI build: APK and AAB signed with the release certificate
  (`CN=Sebastian Ortiz, OU=MoneyRecord`, SHA-256 `e341506e…5b2274a5`), versionCode 92, versionName 1.0.0
  (untagged). Tag `v0.9.0-rc1` on `main` (run 38650210439): GitHub pre-release with the signed `-Signed.apk` and
  `-Signed.aab`, same certificate, versionCode 94, versionName `0.9.0-rc1`.

### 9.3 Permissions and privacy
- Remove unused `INTERNET`, `ACCESS_NETWORK_STATE`, `READ/WRITE_EXTERNAL_STORAGE` from `AndroidManifest.xml`
  (MediaPicker/FilePicker don't need them). Re-add `INTERNET` only with Stage 10 FX rates / cloud backup, behind opt-in.
- `android:allowBackup`: keep, but add `dataExtractionRules` so Android backup includes the DB and receipts and
  excludes `files/.__override__` and caches.
- Privacy section in README: offline, no tracking.
- Done as: removed `INTERNET`, `ACCESS_NETWORK_STATE` (9.4b brought `INTERNET` back for opt-in crash reports), `READ_EXTERNAL_STORAGE` and `READ_MEDIA_IMAGES`. Deviation:
  **`WRITE_EXTERNAL_STORAGE` (maxSdkVersion 32) stays.** MAUI 10.0.90 `MediaPicker.CaptureAsync` calls
  `Permissions.EnsureGrantedAsync<StorageWrite>()` below API 33, and that throws `PermissionException` when the
  permission isn't declared. Picking uses the photo picker / `ACTION_GET_CONTENT` and needs nothing. The Release APK's
  merged manifest (`aapt2 dump permissions`) has only CAMERA, WRITE_EXTERNAL_STORAGE (≤32) and AndroidX's own
  `DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION`. Backup is an allow list rather than excludes, because the rules can't
  match wildcards (`*.bak-v*`): `moneyrecord.db3`, `receipts/` and all shared prefs, in
  `Resources/xml/data_extraction_rules.xml` (API 31+: cloud backup + device transfer) and `backup_rules.xml`
  (`fullBackupContent`, API ≤ 30). The planned 9.4 `logs/` folder is left out automatically. The README Privacy
  section notes the 25 MB cloud-backup quota. Emulator (API 36), checked on a Debug build: camera capture
  (permission prompt, photo attached), gallery pick (system photo picker, no prompt), Share Backup sheet. `bmgr
  backupnow` with LocalTransport backed up 85 KB (the 113 MB `files/.__override__` was not included).
  `bmgr restore 1` after wiping the data brought back the DB, receipt and prefs (no setup screen, the expense
  is listed). The Release APK then installed over the install with `adb install -r` and kept the data. The restore
  also wipes `.__override__`, so a Debug install then needs a reinstall. Review: `AndroidBackupRulesTests` checks
  that all three include lists match `AppPaths.DatabaseFileName`, `ReceiptFiles.FolderName` and the shared prefs
  (the repo-root lookup moved to `TestRepo`, shared with `ThirdPartyComponentTests`).

### 9.4 Logging and crash reports
- Serilog (Apache-2.0) + `Serilog.Sinks.File` to `AppDataDirectory/logs`, 7-day rolling; `ErrorHandler` logs there.
- Settings → "Share logs" (zip via Share API).
- Opt-in crash reporting (Sentry .NET SDK, MIT, or self-hosted GlitchTip), default **off**, asked once after
  first successful week. (Replaces Stage 5.1.)
- Split by agreement (2026-10-14): **9.4a** = local logs + Share logs; **9.4b** = Sentry crash reports (the owner has a
  Sentry account; the DSN goes in a `SENTRY_DSN` GitHub secret passed to CI builds only, so local and fork builds
  never report). 9.4b brings back `INTERNET`, so the README Privacy section and the 9.3 notes change with it.
- 9.4a done as: `Serilog.Extensions.Logging` 10.0.0 + `Serilog.Sinks.File` 7.0.0 (Apache-2.0, About page entry)
  behind the existing `ILogger<T>`, set up in `MauiProgram.ConfigureLogging`. One file a day in `IAppPaths.LogsFolder`
  (`logs/moneyrecord-yyyyMMdd.log`), deleted after 7 days (`retainedFileTimeLimit`). Review: a file rolls over at 1 MB
  (`_001`, ...) and at most 10 files are kept (about 10 MB), so the newest events, such as a final crash, survive an
  error loop (the first version dropped everything past 1 MB a day, including the crash line). Information level in Release (Debug in Debug
  builds), `Microsoft.*` at Warning. Also logged: the version + OS at start, `AppDomain` unhandled exceptions (flushed
  before the process ends) and unobserved task exceptions. Core `LogFiles.CreateArchive` zips the logs, reading
  today's file with shared access while Serilog holds it (tested with a writer that only shares reading). Settings
  has a "Share logs" card (toast when there are no logs yet). The 9.3 backup allow list already leaves `logs/` out.
  Emulator: the card renders, the log file starts with `[INF] MoneyRecord 1.0.0 (1) on Android 16`, and the share
  sheet offers `MoneyRecord_Logs_….zip`.
  Review: zipping runs in `Task.Run` with `IsExporting` set, and `NewCacheFile(prefix, extension)` replaces the
  delete-old-then-timestamp code for the backup, log and CSV shares (the CSV share now cleans up old copies too).
- 9.4b done as: `Sentry.Maui` 6.11.1 (MIT; 6.12.x was under two weeks old), About entry with the sentry-dotnet /
  sentry-java / sentry-native licences (`Sentry.txt`). The `SENTRY_DSN` secret reaches MSBuild as an environment
  variable in the CI build step and is stored as `AssemblyMetadata("SentryDsn")`; local and fork builds have none.
  Core `ICrashReportConsent` / `CrashReportConsent` (prefs `CrashReports.*`, `TimeProvider`): off by default; Home asks
  once (`ConfirmAsync`) when `AskAfterDays` = 7 have passed since the first check; a Settings switch (shown only when a
  DSN exists) also counts as the answer. `MauiProgram.ConfigureCrashReports` calls `UseSentry` only with a DSN **and**
  consent at start (turning it on applies at the next start); `BeforeSend` drops events as soon as it is turned off.
  Options: no default PII, no screenshots, no UI text/titles in breadcrumbs, no session tracking; ILogger errors (e.g.
  `ErrorHandler`) become events. `INTERNET` is back (only permission added; Sentry Android ships
  `io.sentry.auto-init=false`, so nothing starts without `UseSentry`). APK 47.7 → 51.9 MB. README Privacy rewritten.
  Owner: enable "Prevent Storing of IP Addresses" in the Sentry project. Emulator (Debug + placeholder DSN): the
  question appears after backdating `CrashReports.FirstUse` 8 days, "Send reports" saves Decided/Enabled, the
  Settings switch shows on, and the next start loads Sentry.
  Review: `options.Native.EnableBeforeSend = true` (default false in 6.11.1, marked experimental), so Java crashes
  and ANRs from the embedded Android SDK also pass the consent check and "off" really stops sending at once; NDK
  crashes are stored and sent at the next start, when Sentry isn't started without consent. `Answer` removed (the
  `Enabled` setter records the decision), `ShouldAsk` renamed `ShouldAskNow` (its first call starts the week), the DSN
  is read once and passed to `ConfigureCrashReports`.
  CI flake on the review push: a Language-collection test switched `AppResources.Culture` to Spanish while
  `TransactionEnrichmentServiceTests` / `SetupServiceTests` ran, so the test assembly now runs sequentially
  (`CollectionBehavior(DisableTestParallelization = true)`, about 5 s).
  End to end (2026-10-15): the PR's CI APK (real DSN, release key) on the emulator showed the switch; with it on and
  the app restarted, restoring a broken zip ("Failed to restore backup") reached the owner's Sentry project.

### 9.5 Tests and CI
- CI test job on `ubuntu-latest` once Stage 7.1 lands (Core-only tests).
- Add tests: `RecurringTransactionService` (catch-up of missed periods, end date), backup/restore round-trip,
  delete cascades, migrations.
- Coverage report (coverlet, MIT) uploaded as an artifact; no hard threshold yet.
- Done as: the Linux test job and the backup/restore (`BackupRestoreTests`), delete-cascade (`DeleteCascadeTests`) and
  migration tests (`MigrationRunnerTests`, `MoneyAsCentsMigrationTests`) already existed from Stages 6–7, so only
  `RecurringTransactionService` was missing. `RecurringTransactionServiceTests` (7, temp SQLite + `FakeTimeProvider`):
  monthly catch-up on the 31st (→ 28 Feb), due from 00:00 of its day, a second run creates nothing, the end date
  (inclusive) stops occurrences and deactivates, already-ended only deactivates, inactive skipped. Writing them found
  **a bug**: hitting the end date mid catch-up called `DeactivateAsync`, then `SaveAsync(recurring)` wrote the stale
  `IsActive = true` back, so an ended recurrence stayed active (a test covers it). The service now takes a
  `TimeProvider` (registered as `TimeProvider.System`) and `DatabaseInitializer`, and saves each recurrence's new
  transactions plus its advanced schedule in one database transaction, so an interrupted catch-up can't create the
  same occurrence twice. Coverage: `coverlet.collector` 10.1.0 (MIT, loads under Smart App Control here); the CI test
  job collects `XPlat Code Coverage`, writes the line/branch rate to the run summary and uploads
  `coverage.cobertura.xml` as the `coverage-report` artifact. Baseline: MoneyRecord.Core 67.6% lines, 83.7% branches.

### 9.6 Store and F-Droid readiness
- Fastlane metadata folder (`fastlane/metadata/android/{en-US,es-ES}`) with descriptions + screenshots taken via
  `/run-moneyrecord`.
- Check F-Droid inclusion rules (no proprietary deps, reproducible build notes).
- `THIRD-PARTY-NOTICES.md` listing all dependency licences (MDI font Apache-2.0, CommunityToolkit MIT, …).
- Done as (target changed by the owner: **Google Play** first, once the developer account is approved):
  - `fastlane/metadata/android/{en-US,es-419,es-ES}` (the layout `fastlane supply` uploads to Play): title (≤30),
    short (≤80) and full descriptions, `changelogs/default.txt`, 7 phone screenshots each (Home, Add expense,
    Transactions, Spending, Budgets, Cash flow, Trends). es-419 (Latin America) is the main Spanish listing; es-ES
    copies it with Spain wording (nómina, alquiler). Screenshots: emulator at `wm size 1080x2160` (Play rejects
    sides longer than 2:1; 1080x2400 is 2.2:1) with SystemUI demo mode (12:00, full battery, Wi-Fi), demo data
    from a scratch seed script (`#:project` Core, sqlite-net, per-language: fresh install in that language, then
    accounts/transactions/transfers/budgets/recurring mapped by category position), base currency USD with a EUR
    travel account so the per-currency balances show.
  - `PRIVACY.md` (EN + ES) at the repo root: the Play privacy policy URL is
    https://github.com/SebastianOrtizA/MoneyRecord/blob/main/PRIVACY.md (repo is public). It covers on-device data,
    Android backup, opt-in crash reports (what a report holds), logs, permissions, children, contact (GitHub issues).
  - Found while writing it: Sentry sets `User.Id` to a random per-install ID even with `SendDefaultPii = false`
    (`Enricher` / `GlobalRootScopeIntegration`), so the 9.4b README promise "no user ID" was false. `BeforeSend` now
    replaces the user with an empty `SentryUser` (covers Android SDK events too, since `Native.EnableBeforeSend`).
    Review: the Android SDK also puts that ID in `contexts.device.id` (sentry-android-core `DeviceInfoUtil`), so
    `BeforeSend` clears `Contexts.Device.DeviceUniqueIdentifier` too. The policy's "the Sentry project doesn't store
    IP addresses" relies on **Prevent Storing of IP Addresses** in the Sentry project (README says so).
    End to end (CI APK, real DSN, 2026-10-15 01:06 UTC): the "Failed to restore backup" event has no `user.id`, no
    `ip_address` and no `contexts.device.id`, but has `user.geo` (CO / Antioquia / Medellín) that Sentry derives
    server-side even with **Prevent Storing of IP Addresses** on (owner confirmed the project's Data
    Scrubbing settings: scrubber + default scrubbers + prevent IP storage all on), so the policy, README and Data safety now disclose approximate location.
  - `THIRD-PARTY-NOTICES.md` mirrors `ThirdPartyComponent.All` (same order, licence, copyright, URL, licence file);
    `ThirdPartyComponentTests` checks it. Gson (Apache-2.0, via Sentry's Android SDK) added to the Android
    libraries entry. No Play Services / Firebase / other proprietary SDKs in `project.assets.json`.
  - Play Data safety answers (for the owner): data collected = **Crash logs**, **Diagnostics** and **Approximate
    location** (Sentry derives country / region / city from the connection's IP, seen in a real event), optional (user
    choice), not shared (Sentry processes it for the developer: a service provider), encrypted in transit, not linked
    to the user, used for app functionality / diagnostics only; no other data collected (all records stay on device).
    No ads. Target audience: not children. App access: no login needed.
  - F-Droid main repo: effectively blocked for now. The MAUI Android workload is a prebuilt Microsoft binary that
    can't be built from source, which F-Droid's inclusion policy requires (forum threads, no official ruling).
    IzzyOnDroid (signed APKs from GitHub Releases) is the realistic FOSS channel later: its ~30 MB limit needs an
    arm64-only APK (`-r android-arm64`: 29.1 MB vs 49.5 MB universal with x86_64), and apps with sensitive data may
    have no tracking at all, so ask its maintainers whether opt-in Sentry is acceptable (or ship that build without
    a DSN, which already disables Sentry).
  - Not done here: the Play Console itself (account approval pending), a feature graphic (1024x500) and the app icon
    at 512x512 for the listing, upload automation (`fastlane supply` or a CI step).

## Verify
- ~~Tag `v0.9.0-rc1` on a test branch → workflow produces signed APK + AAB with version `0.9.0`.~~ Done on `main`
  (version `0.9.0-rc1`, see 9.2).
- Install the APK over a previous signed build on the emulator: the update keeps data. Partly done in 9.3, which
  installed a locally built Release APK over the Debug install (both use the debug key) and kept the data. The
  release-key run over a release-key install is still open: the emulator's debug-signed app has to be uninstalled first.
- ~~`/run-moneyrecord`: app works with the reduced permission set (camera, gallery pick, backup share).~~ Done in 9.3.

## Done when
Tag builds produce signed, versioned APK/AAB; unused permissions are gone; logs can be shared from Settings.
