# Play Console: Data safety draft (checked against PRIVACY.md, MauiProgram Sentry options, manifest, csproj, 2026-10-31)

Policy and code agree: Sentry is off until consent (SendDefaultPii false, no screenshots, no sessions, BeforeSend gate),
Frankfurter sends only currency codes and dates, ML Kit runs on device, billing is Google's, nothing else goes online.
The form below is a recommendation; the owner submits it (Play Console -> App content -> Data safety).

## Overview questions
- Does the app collect or share required user data types? Yes (see below; all optional or SDK-side).
- Is all collected data encrypted in transit? Yes (HTTPS for Sentry, Frankfurter, ML Kit diagnostics).
- Can users request that data be deleted? Yes: the app keeps nothing server-side; Sentry reports are not linked to a person,
  and the contact for deletion is a GitHub issue (PRIVACY.md "Changes and contact"). Pick "Yes, with a way to request".
- Independent security review: No. Follows Families policy: No (not for children).

## Data types
| Data type | Collected | Shared | Optional | Purpose | Source of the answer |
|---|---|---|---|---|---|
| Crash logs (App info and performance) | Yes | No (Sentry acts as a service provider) | Yes (consent after a week, Settings toggle) | App functionality / Analytics (bug fixing) | Sentry |
| Diagnostics (other app performance data: free memory, device model, Android version) | Yes | No | Yes | App functionality | Sentry |
| Approximate location (country/region/city from IP) | Yes | No | Yes | App functionality | Sentry works it out from the IP, project does not store IPs; PRIVACY.md discloses it, so declare it |
| Device or other IDs (ML Kit per-installation ID) | Yes | No | No (every receipt scan) | App functionality / Analytics | Google ML Kit data disclosure; the SDK's collection counts for the app |
| Diagnostics (ML Kit performance metrics, error codes) | Yes | No | No | App functionality / Analytics | same |

Not collected: name, email, financial info (amounts, accounts and receipts stay on the device and are never sent), photos,
contacts, files, purchase history (Google Play Billing is exempt from the form; the Direct build has no billing).

## Judgement calls to confirm
1. ML Kit: Google's page lists the diagnostics above; declaring them as "collected, not shared" matches PRIVACY.md. Declaring "shared"
   is the safer reading if you treat Google as a third party; Play's own guidance says SDK data sent to Google as the SDK provider
   still counts as collected. Keeping the PRIVACY.md wording and the form identical is what matters.
2. Frankfurter: only currency codes and dates plus the IP of the request. Not declared as a data type (no user data is sent
   deliberately); PRIVACY.md already says the IP reaches that server. Add "IP address" under Device or other IDs only if you want to be strict.
3. Financial info: "User payments"/"Purchase history" stay unchecked; tips are processed by Google.
