#!/usr/bin/env bash
set -euo pipefail
package="$1"
output=apps/lunet2d/dist/rpg-sonhos
apk="$output/emulator.apk"
aapt=$(find "$ANDROID_HOME/build-tools" -name aapt | sort -V | tail -n1)
apksigner=$(find "$ANDROID_HOME/build-tools" -name apksigner | sort -V | tail -n1)
zipalign=$(find "$ANDROID_HOME/build-tools" -name zipalign | sort -V | tail -n1)
"$apksigner" verify "$output/RPG-dos-Sonhos-arm64.apk"
"$zipalign" -c 4 "$output/RPG-dos-Sonhos-arm64.apk"
adb shell wm size 480x800
adb shell wm density 160
adb install -r "$apk"
activity=$("$aapt" dump badging "$apk" | sed -n "s/^launchable-activity: name='\([^']*\)'.*/\1/p" | head -n1)
test -n "$activity"
adb logcat -c
adb shell am start -W -n "$package/$activity"
started=false
for attempt in {1..15}; do
  adb logcat -d > "$output/android-logcat.txt"
  if grep -Fq 'RPG:seed=20261030; slice=1; offline' "$output/android-logcat.txt"; then started=true; break; fi
  sleep 2
done
test "$started" = true
adb shell pidof "$package"
adb exec-out screencap -p > "$output/android-screen.png"
# Lifecycle exercises the real Android host, separate from human touch acceptance.
adb shell input keyevent KEYCODE_HOME
sleep 2
adb logcat -d > "$output/android-paused-logcat.txt"
grep -Fq 'RPG: Estado gravado' "$output/android-paused-logcat.txt"
adb shell am start -W -n "$package/$activity"
sleep 2
adb exec-out screencap -p > "$output/android-resumed.png"
adb shell am force-stop "$package"
adb logcat -c
adb shell am start -W -n "$package/$activity"
sleep 4
adb logcat -d > "$output/android-restarted-logcat.txt"
grep -Fq 'RPG:seed=20261030; slice=1; offline' "$output/android-restarted-logcat.txt"
adb shell pidof "$package"
adb exec-out screencap -p > "$output/android-restarted.png"
adb shell am force-stop "$package"
