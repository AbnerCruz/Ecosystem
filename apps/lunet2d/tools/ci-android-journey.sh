#!/usr/bin/env bash
# Android emulator contract of LUNET-450. Executed in ONE shell, since
# android-emulator-runner runs each 'script:' line in its own /bin/sh process.
set -euo pipefail

journey=apps/lunet2d/dist/journey
package="$(cat "$journey/journey-package-id.txt")"
apk="$journey/journey.apk"
test -s "$apk"

adb install -r "$apk"
adb shell pm path "$package" | grep -q '^package:'
aapt="$(find "$ANDROID_HOME/build-tools" -name aapt | sort -V | tail -n1)"
test -n "$aapt"
activity="$("$aapt" dump badging "$apk" | sed -n "s/^launchable-activity: name='\\([^']*\\)'.*/\\1/p" | head -n1)"
test -n "$activity" || { echo 'Activity de lançamento ausente'; exit 1; }

adb logcat -c
adb shell am start -W -n "$package/$activity" | tee "$journey/android-launch.txt"
grep -q 'Status: ok' "$journey/android-launch.txt"

running=false
for attempt in 1 2 3 4 5 6 7 8 9 10; do
    adb logcat -d > "$journey/android-logcat.txt"
    if adb shell pidof "$package" | grep -q '[0-9]' &&
       grep -Fq 'Jornada editada iniciou' "$journey/android-logcat.txt"; then
        running=true
        break
    fi
    sleep 2
done

if [[ "$running" != true ]]; then
    echo 'Jogo não iniciou, não carregou assets ou não escreveu o log de Game.cs'
    tail -n 100 "$journey/android-logcat.txt"
    exit 1
fi

adb shell screencap -p /sdcard/journey.png
adb pull /sdcard/journey.png "$journey/android-screen.png"
adb shell am force-stop "$package"
echo "Jornada E2E Android: APK $package instalado, iniciado, PNG e tilemap carregados."
