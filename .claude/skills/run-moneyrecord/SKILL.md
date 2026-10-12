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
$D shot home     # prints the PNG path; open it with Read and LOOK at it
```

Screenshots go to `$TMPDIR/moneyrecord-shots/` (here `C:/devkitPro/msys2/tmp/moneyrecord-shots/`). The path is printed in Windows form so Read can open it.

| command | what it does |
|---|---|
| `boot` | start the emulator if none is attached, wait for `sys.boot_completed`, wake and keep the screen on |
| `deploy [clean]` | `dotnet build -t:Run -f net10.0-android -c Debug`; `clean` uninstalls first |
| `launch` | relaunch the app, print the focused window |
| `shot NAME` | `screencap` to `NAME.png`, print the path |
| `tap X Y` / `swipe X1 Y1 X2 Y2 [ms]` | input in device pixels (1080x2400) |
| `text "STR"` | type into the focused field (spaces are handled) |
| `key CODE` / `back` | key events, e.g. `key 67` (backspace), `key 123` (move to end) |
| `menu` | open the Shell flyout (hamburger at 72,210) |
| `locale es-ES` / `locale reset` | per-app language, then relaunch |
| `fresh` | delete `moneyrecord.db3*`, receipts, backups and prefs (keeps the install working) |
| `crash [N]` | last N fatal/exception lines from logcat |
| `stop` | kill the emulator |

**Coordinates:** screenshots read back at 900x2000 in the Read tool, so multiply what you see by **1.2** to get the value for `tap`. Known spots (device pixels):
- Menu items: Home y=203, Expense Report 335, Income Report 467, Trends 599, Budget 731, Recurring 863, Transfers 995, Manage Accounts 1127, Manage Categories 1259, Settings 1391 (x≈300).
- Home: total balance card (540,504) opens the account balances popup. Round buttons: income (319,1249), transfer (540,1249), expense (761,1249).
- Alert OK button: about (906, alert bottom − 70).

`uiautomator dump` returns nothing useful for MAUI views, so drive from screenshots.

Example flow (verified): add an account.
```bash
$D menu; sleep 2; $D tap 340 1127; sleep 3   # Manage Accounts
$D tap 540 615; $D text "My Bank"; $D back    # name field
$D shot accounts
```

## Run (human path)

```powershell
dotnet build MoneyRecord/MoneyRecord.csproj -t:Run -f net10.0-android   # installs and opens on the running emulator
```

## Test

```powershell
dotnet test MoneyRecord.Tests/MoneyRecord.Tests.csproj    # 284 pass, ~2 s (plain net10.0, references MoneyRecord.Core only)
```

## Gotchas

- **Every boot loses the app.** The AVD resumes its quick-boot snapshot, which predates the install, and `-no-snapshot-save` keeps it that way (a clean, repeatable device). Symptom: `launch` shows `NexusLauncherActivity` and logcat says `Invalid packageName`. Always run `deploy clean` after `boot`.
- **`pm clear` bricks a Debug install.** Fast deployment keeps the assemblies in `files/.__override__`; clearing app data deletes them and the app aborts on start. Use `fresh`, which removes only the app's own files. If it's already broken, plain `deploy` does nothing (the build is up to date), so use `deploy clean`.
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
