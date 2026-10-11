# Stage 5: Post-Release

**Status**: Superseded by Stages 6–10 (review of 2026-10-11)
**Depends on**: Stage 4 (app is released)
**Complexity**: Variable

> Where each item went: 5.1 crash reporting → Stage 9.4 (opt-in, no analytics);
> 5.2 cloud sync → Stage 10.5 (cloud backup first; two-way sync deferred);
> 5.3 more languages → still open, do after Stage 6.6 removes hard-coded strings;
> 5.4 feedback → still open; 5.5 widget → Stage 10.9.

## Goal
Iterate based on real-world usage and add cloud features.

## Tasks

### 5.1 Crash Reporting & Analytics
- Integrate Sentry or AppCenter for crash reporting
- Add basic usage analytics (screen views, feature usage)
- Monitor SQLite performance in production

### 5.2 Cloud Sync (Optional)
- Azure or Firebase backend
- Offline-first architecture with sync-on-connect
- Conflict resolution: last-write-wins for v1
- User authentication (Microsoft Account or Google Sign-In)
- Encrypted data transit

### 5.3 Additional Localizations
- Portuguese, French, German based on user demand
- Leverage existing `LocalizationService` + `.resx` pattern
- Community contributions via translation files

### 5.4 User Feedback Integration
- In-app feedback form (link to GitHub Issues or custom endpoint)
- App store review prompt (after N transactions or N days of use)
- Feature request voting

### 5.5 Widget Support (Android)
- Home screen widget showing total balance
- Quick-add transaction widget
- Uses Android native widget APIs via platform-specific code
