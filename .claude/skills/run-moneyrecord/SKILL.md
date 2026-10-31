---
name: run-moneyrecord
description: Build, run, launch and drive the MoneyRecord MAUI app on the local Android emulator. Use when asked to run or start the app, deploy it, take a screenshot of its UI, check a UI/UX change on device, test it in Spanish, reset it to first-run state, or read a crash log.
---

MoneyRecord is a .NET 10 MAUI app; the agent path is the Android emulator driven over adb by
`.claude/skills/run-moneyrecord/driver.sh` (boot, deploy, launch, screenshot, tap/type, locale, reset).
All paths are relative to the repo root `D:\Projects\MoneyRecord`. Run the driver from the **Bash** tool.

## Prerequisites (this Windows machine)

Already installed. Verify with these if something fails:

```powershell
dotnet --version                     # 10.0.401
dotnet workload list                 # android, ios, maccatalyst, maui-windows
& "C:\Program Files (x86)\Android\android-sdk\emulator\emulator.exe" -list-avds   # pixel_7_-_api_36_0
```

The SDK is the Visual Studio one at `C:\Program Files (x86)\Android\android-sdk`, not `%LOCALAPPDATA%\Android\Sdk`.
The driver reads `ANDROID_SDK`, `AVD`, `DOTNET` and `SHOTS_DIR` if you need to override them.

## Run (agent path)

```bash
D=.claude/skills/run-moneyrecord/driver.sh
$D boot          # start the AVD detached (or reuse it), wait for boot, wake the screen
$D deploy clean  # uninstall + build + install + start (Debug, ~15 s). Always after boot, see Gotchas
$D fresh         # optional: wipe DB/prefs -> first-run seed data
$D launch        # force-stop + start; prints mCurrentFocus (should end in MainActivity)
$D where         # text only: resumed activity, app running?, fatal count
```

### Screenshot policy (saves tokens: an image costs far more than text)

Do **not** screenshot after every step. Walk known paths blind and check with text.

1. **Known path** (listed in "Known paths" below) → `$D go ROUTE` / `tap` / `text`, then `$D where`. No image.
2. **Confirm the screen** only when it matters → `$D title` (270 px page-title band, a few tokens).
3. **Verify an outcome** (a value, a list, a layout) → `$D small NAME 450 TOP% BOTTOM%` cropped to the band you need
   (e.g. `small x 450 10 60`). Full `shot` only for pixel detail (alignment, colors, store screenshots).
4. **New feature or changed screen** (code you just wrote, a page not in Known paths) → look once with `small`
   or `shot`, **then add its route/coordinates to "Known paths" below and to `go` in `driver.sh` in the same
   session**, so the next run is blind.
5. **A blind step went wrong** (`where` shows another activity or a crash, `title` shows the wrong page) → one `small`,
   fix the stored coordinates, move on. Reset between routes with `$D go home`.
6. Stack several `small` crops into one image (PowerShell System.Drawing) when you must compare many screens.

Screenshots go to `$TMPDIR/moneyrecord-shots/` (here `C:/devkitPro/msys2/tmp/moneyrecord-shots/`). The path is printed in Windows form so Read can open it.

| command | what it does |
|---|---|
| `boot` | start the emulator if none is attached, wait for `sys.boot_completed`, wake and keep the screen on |
| `deploy [clean]` | `dotnet build -t:Run -f net10.0-android -c Debug`; `clean` uninstalls first |
| `launch` | relaunch the app, print the focused window |
| `shot NAME` | full `screencap` to `NAME.png`, print the path (expensive: use sparingly) |
| `small [NAME] [W=450] [TOP% BOT%]` | scaled and optionally cropped screenshot, ~4x cheaper. Coordinates in it: device px = crop px × (1080/W), plus the crop's top offset |
| `title [NAME]` | page-title band only (270 px wide), the cheapest "which screen am I on" |
| `where` | text only: resumed activity, app running, fatal-exception count |
| `go ROUTE` | blind known route (see Known paths), 2 s between taps (`GO_WAIT`) |
| `tap X Y` / `swipe X1 Y1 X2 Y2 [ms]` | input in device pixels (1080x2400) |
| `text "STR"` | type into the focused field (spaces are handled) |
| `key CODE` / `back` | key events, e.g. `key 67` (backspace), `key 123` (move to end) |
| `tab NAME` | tap a bottom tab: `home`, `transactions`, `reports`, `budgets`, `more` |
| `locale es-ES` / `locale reset` | per-app language, then relaunch |
| `fresh` | delete `moneyrecord*.db3*` (plain and encrypted), receipts, backups and prefs (keeps the install working) |
| `crash [N]` | last N fatal/exception lines from logcat |
| `stop` | kill the emulator |

## Known paths (verified 2026-10-31, 1080x2400; keep this section and `go` in driver.sh in sync)

Scale for taps: full `shot` reads back at 900x2000, so ×1.2; `small` at width W, ×(1080/W) and add the crop's top offset.

| route (`go X`) | taps | lands on (title) |
|---|---|---|
| `home` `transactions` `reports` `budgets` `more` | bottom tab y=2262, x = 108 / 323 / 539 / 755 / 971 | tab root |
| `accounts` `categories` `payees` `recurring` `goals` `savings` `debts` `trips` `parties` `transfers` `settings` | More tab, then row at x=300, y = 374 + 139 × (row−1) in this order: 374 Accounts, 513 Categories, 652 Payees, 791 Recurring, 930 Goals, 1069 Savings challenge, 1208 Debts, 1346 Trips, 1485 Parties, 1624 Transfers, 1763 Settings | Manage Accounts, Categories, Payees, Recurring, Savings goals, Savings challenge, Debts, Trips, Parties, Transfers, Settings |
| `add-expense` `add-income` `add-transfer` | Add button (959,2069), then sheet item (300,1621) / (300,1801) / (300,1980) | Add Expense / Income / Transfer |

A new More row shifts every row below it by 139: update both this table and `go`. First run of a fresh install shows the setup
wizard: Skip is at the top right (982,230).

Other known spots (device pixels):
- Home: total balance card (540,504) opens Manage Accounts (a currency line, with several currencies, filters it); Recent "See all" is at the card's top right. Reports tab: Expenses / Incomes / Trends top tabs at y≈358.
- Add Expense / Income form: the amount is focused with the keypad open (type, then `back` to close it); category grid
  row 1 at y=760 (x = 175 / 420 / 663 / 907), row 2 at y=980; Save is pinned at (540,2242); "More details" (540,1698)
  opens description, tags, receipt and recurrence.
- Alert OK button: about (906, alert bottom − 70).

`uiautomator dump` returns nothing useful for MAUI views, so a screen can only be read from an image: follow the screenshot policy above.

Example flow (verified): add an account, one cheap image at the end.
```bash
$D go accounts                                  # blind; confirm with `$D title` only if unsure
$D tap 540 615; $D text "My Bank"; $D back     # name field
$D small accounts 450 5 60                      # the one look that matters
```

## Run (human path)

```powershell
dotnet build MoneyRecord/MoneyRecord.csproj -t:Run -f net10.0-android   # installs and opens on the running emulator
```

## Test

```powershell
dotnet test MoneyRecord.Tests/MoneyRecord.Tests.csproj    # 308 pass, ~2 s (plain net10.0, references MoneyRecord.Core only)
```

## Gotchas

- **Every boot loses the app.** The AVD resumes its quick-boot snapshot, which predates the install, and `-no-snapshot-save` keeps it that way (a clean, repeatable device). Symptom: `launch` shows `NexusLauncherActivity` and logcat says `Invalid packageName`. Always run `deploy clean` after `boot`.
- **`pm clear` bricks a Debug install.** Fast deployment keeps the assemblies in `files/.__override__`; clearing app data deletes them and the app aborts on start. Use `fresh`, which removes only the app's own files. If it's already broken, plain `deploy` does nothing (the build is up to date), so use `deploy clean`.
- **`bmgr restore` also bricks a Debug install** (Android clears the data dir before a full restore, `.__override__` included). To keep the restored data, `adb install -r` a local Release `-Signed.apk` (`dotnet publish -c Release`, debug-key signed like the Debug build, assemblies inside) instead of `deploy clean`. Backup test: `bmgr enable true`, `bmgr transport com.android.localtransport/.LocalTransport`, `bmgr backupnow PKG`, then `bmgr restore 1 PKG` (the emulator is a production build, so no `adb root`).
- **Plain `adb install` of the `-Signed.apk` crashes** for the same reason: the Debug APK has no assemblies inside. Install only via `-t:Run`.
- **Start the emulator detached.** A `nohup … &` child of the Bash tool gets killed later, and `cmd //c start /min` has `/min` rewritten into a path by msys. The driver uses `powershell Start-Process`.
- **A fresh adb server can't see the emulator for about 2 s** (`error: no emulator detected`). `boot` and `stop` start the server and wait first.
- **The screen starts asleep**, so a screenshot is all black. `boot` and `launch` wake it.
- **`dotnet` isn't on the Bash tool's PATH** (it's the devkitPro msys bash). The driver calls `/c/Program Files/dotnet/dotnet.exe`; run `dotnet test` from PowerShell.
- **`adb logcat -d -b crash,main` can hang.** The driver uses `-b crash -b main -t 3000` with a 20 s timeout.
- After `locale es-ES`, a "keyboard language changed" toast covers the bottom of the screen for a few seconds.

## Troubleshooting

- **`dotnet test` says "Una directiva de Control de aplicaciones bloqueó este archivo" (0x800711C7)**: Smart App Control is on and has blocked
  a freshly built DLL by hash. Builds are deterministic, so plain rebuilds keep the same blocked hash. Force a new one:
  `dotnet build MoneyRecord.Tests/MoneyRecord.Tests.csproj --no-incremental -p:Deterministic=false`, then `dotnet test --no-build`.
  If the blocked file is a **NuGet package DLL** (the message names e.g. `AwesomeAssertions.dll`), rebuilding can't help: its
  hash is fixed. Try another version of that package (AwesomeAssertions 9.4.0 loads; 9.0.0, 9.5.0 and 9.6.0 are blocked).

- **`launch` prints `NexusLauncherActivity`** and `crash` shows `No assemblies found in '/data/user/0/com.sebastianortiz.moneyrecord/files/.__override__/x86_64'`: fast-deploy files are gone. Run `$D deploy clean`.
- **`crash` shows `Invalid packageName: com.sebastianortiz.moneyrecord`**: not installed (fresh boot). Run `$D deploy clean`.
- **`El sistema no puede encontrar el archivo C:/devkitPro/msys2/min`**: an old driver version that used `cmd start /min`. Use the current `boot`.
- **`- waiting for device -` hangs**: the emulator process died. Run `$D boot` again.
