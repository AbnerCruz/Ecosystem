#!/usr/bin/env bash
# LUNET-483: capture the IDE screens on an emulator so UI changes are reviewed visually, not blind.
# Runs in ONE shell (android-emulator-runner runs each script line separately).
set -euo pipefail

out=apps/lunet2d/dist/ui-screens
apk="$(find apps/lunet2d/src/Lunet.Android/bin -name '*-Signed.apk' | head -n1)"
test -s "$apk"
mkdir -p "$out"
adb install -r "$apk"
aapt="$(find "$ANDROID_HOME/build-tools" -name aapt | sort -V | tail -n1)"
activity="$("$aapt" dump badging "$apk" | sed -n "s/^launchable-activity: name='\\([^']*\\)'.*/\\1/p" | head -n1)"
package=io.lunet.studio
# Wait for a settled system: boot completed, launcher idle, no ANR dialogs over the app.
adb wait-for-device
until [ "$(adb shell getprop sys.boot_completed | tr -d '\r')" = "1" ]; do sleep 2; done
sleep 20
adb shell input keyevent KEYCODE_WAKEUP || true
adb shell wm dismiss-keyguard || true
adb shell am broadcast -a android.intent.action.CLOSE_SYSTEM_DIALOGS >/dev/null || true
adb shell settings put global window_animation_scale 0 || true
adb shell settings put global transition_animation_scale 0 || true

shoot() {
    local name="$1" demo="$2" wait="${3:-8}"
    adb shell am force-stop "$package"
    adb shell am broadcast -a android.intent.action.CLOSE_SYSTEM_DIALOGS >/dev/null || true
    adb shell am start -W -n "$package/$activity" --es lunet.demo "$demo" >/dev/null
    sleep "$wait"
    adb shell screencap -p "/sdcard/$name.png"
    adb pull "/sdcard/$name.png" "$out/$name.png" >/dev/null
}

# Warm-up: first start extracts assemblies and JITs Roslyn; not captured.
adb shell am start -W -n "$package/$activity" --es lunet.demo projects >/dev/null
sleep 25

# Portrait phone
shoot 01-projects projects 10
shoot 02-workspace workspace 12
shoot 03-ai ai 12
shoot 04-palette palette 12
shoot 05-studio studio 12
shoot 06-notes notes 12
shoot 07-note note 12
shoot 08-sprite sprite 12
shoot 09-ai-settings ai-settings 12
shoot 10-problems problems 12
shoot 11-files files 12
shoot 12-run run 25
shoot 13-image image 12

adb logcat -d > "$out/logcat.txt" || true
grep -E "FATAL EXCEPTION|AndroidRuntime" -A20 "$out/logcat.txt" | head -80 || true
echo "Capturas em $out"
