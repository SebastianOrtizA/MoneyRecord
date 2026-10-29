# Stage 19: Support / tips (Google Play Billing only)

**Status:** 19 in review (branch stage-19-donations). Open items for the owner: Play Console payments profile + the three in-app
products, licence-tester testing on a Play-installed build.

## Context
The owner wants users to be able to give something back. Nothing unlocks: a tip changes nothing for givers or non-givers.
Google Play counts a tip to the app's own developer as an in-app payment, and an external donation link (PayPal, Ko-fi, …) is
allowed only for tax-exempt organizations (AnkiDroid, WireGuard and StreetComplete were all pushed to remove theirs). So tips go through
**Google Play Billing**, in the Play build only. The owner chose to drop PayPal: the GitHub / F-Droid APK has no Support screen.

## Design
- **Two Android distributions** (`Distribution` MSBuild property, default `Direct`). `-p:Distribution=Play` defines `PLAY_STORE` and adds
  `Plugin.InAppBilling` 10.0.0 (MIT, Billing Library 8); only that build has the BILLING permission. CI builds the APK (`publish/direct`) and the
  AAB (`publish/play`) separately, each with its own R8 `mapping.txt`; the Play upload reads `release/play`.
- **Core** (`MoneyRecord.Core`): `ITipJar` (`IsAvailable`, `GetTipsAsync`, `TipAsync` → `TipOutcome`, `ConsumePendingAsync`), `TipOption`,
  `SupportLinks.TipProducts` (`tip_small`, `tip_medium`, `tip_large`), `SupportViewModel` (tips list, busy flag, thank-you / pending toasts,
  errors via `IErrorHandler`).
- **App**: `PlayTipJar` (Platforms/Android, `#if PLAY_STORE`: purchase then consume at once so the same tip can be given again; a semaphore
  serializes the billing connection), `NoTipJar` elsewhere, `SupportPage`, a More row and an About button shown only when `ITipJar.IsAvailable`,
  `ConsumePendingAsync()` at start (a payment that settled after the app closed).
- Strings in en / es / pt / fr / it, licence entries (`ThirdPartyComponent`, `THIRD-PARTY-NOTICES.md`, `InAppBilling.txt`), PRIVACY.md, README.

## Owner setup (not code)
1. Play Console → payments profile (merchant account), and declare the income as taxable.
2. Upload one Play build first: Play only lets you create products once an uploaded build declares `com.android.vending.BILLING`.
3. Monetize → In-app products: `tip_small`, `tip_medium`, `tip_large` (one-time, consumable; e.g. 0.99 / 2.99 / 4.99 USD).
4. License testing: add the test Google account(s) so purchases are free and refunded.
5. Review the Play listing's Data safety form (purchases are processed by Google; the app stores nothing about them).

## Verification
- `dotnet test` (SupportViewModelTests, resource parity, licence list).
- Direct build: no Support row in More, no button in About, no BILLING in the merged manifest.
- Play build (`-p:Distribution=Play`): BILLING in the manifest, Release AAB with R8 keeps the billing public API (checked in `mapping.txt`).
- Real purchases need a Play-installed build and a licence tester: buy each tip, buy the same one again (consumed), slow test card (pending).
