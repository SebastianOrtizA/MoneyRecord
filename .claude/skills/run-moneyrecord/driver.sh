#!/usr/bin/env bash
# Drive MoneyRecord on the local Android emulator via adb.
# Usage: .claude/skills/run-moneyrecord/driver.sh <command> [args]
# Run from the repo root (D:\Projects\MoneyRecord). See SKILL.md for the command table.
set -euo pipefail

SDK="${ANDROID_SDK:-/c/Program Files (x86)/Android/android-sdk}"
ADB="$SDK/platform-tools/adb.exe"
EMU="$SDK/emulator/emulator.exe"
DOTNET="${DOTNET:-/c/Program Files/dotnet/dotnet.exe}"
AVD="${AVD:-pixel_7_-_api_36_0}"
PKG="com.sebastianortiz.moneyrecord"
CSPROJ="MoneyRecord/MoneyRecord.csproj"
SHOTS="${SHOTS_DIR:-${TMPDIR:-/tmp}/moneyrecord-shots}"

adb() { "$ADB" "$@"; }

# Print a path the Read tool can open (Windows form when cygpath exists).
winpath() { command -v cygpath >/dev/null 2>&1 && cygpath -m "$1" || echo "$1"; }

wake() {
  adb shell input keyevent KEYCODE_WAKEUP
  adb shell wm dismiss-keyguard
  adb shell svc power stayon true
}

booted() { [ "$(adb shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')" = "1" ]; }

cmd="${1:-help}"; shift || true
case "$cmd" in
  boot)
    # Starts the AVD detached if nothing is attached, then waits for boot (up to ~3 min).
    adb start-server >/dev/null 2>&1; sleep 2   # a fresh adb server needs a moment to see a running emulator
    if ! adb devices | grep -q 'emulator-'; then
      # Detach via PowerShell Start-Process: a nohup'd background child of this shell dies with it,
      # and `cmd //c start /min` gets its /min mangled into a path by msys.
      powershell.exe -NoProfile -Command "Start-Process -FilePath '$(winpath "$EMU")' -ArgumentList '-avd','$AVD','-no-snapshot-save','-no-boot-anim' -WindowStyle Minimized"
    fi
    adb wait-for-device
    for _ in $(seq 1 90); do booted && break; sleep 2; done
    booted || { echo "emulator did not finish booting" >&2; exit 1; }
    wake
    echo "booted: $(adb devices | grep emulator- | cut -f1)"
    ;;
  deploy)
    # Debug builds use fast deployment: only -t:Run installs a working app (plain adb install crashes).
    # `deploy clean` uninstalls first: an up-to-date incremental build won't re-push missing assemblies.
    if [ "${1:-}" = "clean" ]; then adb uninstall "$PKG" >/dev/null 2>&1 || true; fi
    # With a phone plugged in too, set ANDROID_SERIAL=emulator-5554 so adb and the MSBuild installer both pick the emulator
    "$DOTNET" build "$CSPROJ" -t:Run -f net10.0-android -c Debug -v q -nologo ${ANDROID_SERIAL:+"-p:AdbTarget=-s $ANDROID_SERIAL"}
    ;;
  launch)
    wake
    adb shell am force-stop "$PKG"
    adb shell monkey -p "$PKG" -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1
    sleep "${LAUNCH_WAIT:-6}"
    adb shell dumpsys window | grep -o "mCurrentFocus=.*" | tr -d '\r'
    ;;
  shot)
    mkdir -p "$SHOTS"
    f="$SHOTS/${1:-shot-$(date +%H%M%S)}.png"
    adb exec-out screencap -p > "$f"
    winpath "$f"
    ;;
  tap)   adb shell input tap "$1" "$2" ;;
  swipe) adb shell input swipe "$1" "$2" "$3" "$4" "${5:-400}" ;;
  text)  adb shell input text "${1// /%s}" ;;
  key)   adb shell input keyevent "$1" ;;
  back)  adb shell input keyevent KEYCODE_BACK ;;
  tab)   # Bottom tab bar (1080x2400 screen)
    case "$1" in
      home) x=108 ;; transactions) x=323 ;; reports) x=539 ;; budgets) x=755 ;; more) x=971 ;;
      *) echo "tab: home|transactions|reports|budgets|more" >&2; exit 2 ;;
    esac
    adb shell input tap "$x" 2262 ;;
  locale)
    # Per-app language (Android 13+). "reset" returns to the system language. Relaunches the app.
    if [ "${1:-}" = "reset" ]; then adb shell cmd locale set-app-locales "$PKG" --locales ""
    else adb shell cmd locale set-app-locales "$PKG" --locales "$1"; fi
    "$0" launch
    ;;
  fresh)
    # First-run state: delete the DB, receipts, backups and prefs (seed data is recreated on next launch).
    # NOT `pm clear`: that also wipes files/.__override__ (Debug fast-deployment assemblies) -> app aborts.
    adb shell am force-stop "$PKG"
    adb shell run-as "$PKG" sh -c "'rm -rf files/moneyrecord.db3* files/moneyrecord-encrypted.db3* files/receipts files/Backups shared_prefs'"
    echo "app data cleared"
    ;;
  crash)
    # Last .NET / Android crash lines for the app.
    timeout 20 "$ADB" logcat -d -b crash -b main -t 3000 | grep -iE "FATAL|AndroidRuntime|mono-rt|Exception" | tail -n "${1:-40}"
    ;;
  stop)
    adb start-server >/dev/null 2>&1; sleep 2
    adb emu kill
    ;;
  help|*)
    sed -n '2,4p' "$0"; echo "commands: boot deploy launch shot tap swipe text key back tab locale fresh crash stop"
    ;;
esac
