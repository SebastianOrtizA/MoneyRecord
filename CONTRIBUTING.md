# Contributing

MoneyRecord is Android first. Windows follows; iOS and Mac Catalyst are community, best-effort (see README → Platforms). A feature
ships on Android first, and a platform that cannot do it must keep working without it.

## Platform code

All testable logic lives in `MoneyRecord.Core` (plain `net10.0`). Anything that needs the OS goes behind an interface in
`MoneyRecord.Core/Services/Interfaces/`, with an Android implementation in `MoneyRecord/Platforms/Android/` and a `No*` fallback
registered for the other platforms in `MauiProgram.cs`. When a feature cannot work on a platform, its UI hides through the
interface's `IsSupported` (or equivalent) instead of failing.

| Interface | Purpose | Android | Windows | Fallback |
|---|---|---|---|---|
| `IDeviceLock` | Biometric / screen-lock unlock | BiometricPrompt | Windows Hello | `NoDeviceLock` |
| `IDatabaseKeyStore` | Key for the encrypted database | SecureStorage | DPAPI | `NoDatabaseKeyStore` |
| `IBackupPasswordStore` | Backup zip password | SecureStorage | DPAPI | `NoBackupPasswordStore` |
| `INotificationService` | Reminders and alerts | Notifications + WorkManager | Toasts + hourly timer while open | `NoNotificationService` |
| `IReceiptScanner` | Receipt OCR | ML Kit | Windows.Media.Ocr | `NoReceiptScanner` |
| `IHomeScreenWidgets` | Home-screen widget refresh | App widget | none | `NoHomeScreenWidgets` |
| `IBackupFolder` | Automatic backup destination | Storage Access Framework | Plain folder path | `NoBackupFolder` |
| `IScreenProtection` | Hide content in recents / screenshots | `FLAG_SECURE` | `SetWindowDisplayAffinity` | `NoScreenProtection` |

## Before a pull request

- `dotnet test MoneyRecord.Tests/MoneyRecord.Tests.csproj` passes (it runs on Linux in CI).
- User-visible strings exist in every language (`ResourceParityTests`); see `CLAUDE.md` → Localization.
- Money is integer cents; never add different currencies unconverted.
- The project is AGPL-3.0; new dependencies need a compatible license (see `THIRD-PARTY-NOTICES.md`).
