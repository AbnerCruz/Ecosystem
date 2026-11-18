#!/usr/bin/env bash
# Android UI journey on the CI emulator (urbe-checks.yml → android-runtime-smoke), run from the repository root.
# The emulator build (URBE_EMULATOR) opens the example vault copied (run-as) into the app's private files and accepts
# "--es urbe.open <file> --ez urbe.play true". It leaves screenshots, a screen recording and the logcat in $OUT.
# Emulator evidence is not DEVICE validation (NN-017): the moto g32 test stays with the owner.
set -u
PKG=app.urbe.nativepreview
OUT=apps/urbe/csharp/artifacts/android-smoke
mkdir -p "$OUT"
shot() { adb exec-out screencap -p > "$OUT/$1.png"; }
alive() { adb shell pidof "$PKG" > /dev/null || { echo "::error::$PKG is not running after: $1"; adb logcat -d -v threadtime > "$OUT/logcat.txt"; exit 1; }; }

adb install -r "$(find apps/urbe/csharp/artifacts/emulator -name '*-Signed.apk' | head -1)" || exit 1
# into the app's own files (the debuggable emulator build allows run-as): the app reads it like any folder
adb push apps/urbe/csharp/tests/fixtures/e2e-vault /data/local/tmp/ > /dev/null || exit 1
adb shell chmod -R a+rX /data/local/tmp/e2e-vault
adb shell run-as "$PKG" mkdir -p files
adb shell run-as "$PKG" cp -r /data/local/tmp/e2e-vault files/ || exit 1
adb logcat -c

# 1. opening: the example vault's notes
adb shell monkey -p "$PKG" -c android.intent.category.LAUNCHER 1 > /dev/null
sleep 12
adb logcat -d -v threadtime > "$OUT/startup-logcat.txt"
alive "launch"
grep "urbe-e2e" "$OUT/startup-logcat.txt"
grep -q "urbe-e2e.*vault de exemplo: [1-9]" "$OUT/startup-logcat.txt" || { echo "::error::the example vault did not reach the app"; shot 01-notas; exit 1; }
shot 01-notas
adb shell uiautomator dump /sdcard/ui.xml > /dev/null 2>&1 && adb pull /sdcard/ui.xml "$OUT/01-notas.xml" > /dev/null 2>&1

# 2. the video publication opens in the Estúdio (not as JSON) and plays — recorded
adb shell screenrecord --time-limit 24 /sdcard/urbe-video.mp4 &
REC=$!
sleep 1
adb shell am start --activity-single-top -a android.intent.action.MAIN -c android.intent.category.LAUNCHER -p "$PKG" \
  --es urbe.open "Publicações/Demo.publication.json" --ez urbe.play true > /dev/null
sleep 4
shot 02-estudio
# scroll the Estúdio down to the preview, the timeline and the scenes
adb shell input swipe 540 1700 540 600 400
sleep 3
shot 03-video-tocando
sleep 6
shot 04-video-cena-2
adb shell input swipe 540 1700 540 900 400
sleep 3
shot 05-cenas
wait $REC
adb pull /sdcard/urbe-video.mp4 "$OUT/estudio-video.mp4" > /dev/null
alive "video playback"

# 3. back to the notes and resume from Home (lifecycle)
adb shell input keyevent KEYCODE_BACK
sleep 2
adb shell input keyevent KEYCODE_HOME
sleep 2
adb shell monkey -p "$PKG" -c android.intent.category.LAUNCHER 1 > /dev/null
sleep 5
alive "resume"
shot 06-retomado
adb logcat -d -v threadtime > "$OUT/logcat.txt"
if grep -E 'FATAL EXCEPTION|Fatal signal|ANR in app.urbe' "$OUT/logcat.txt"; then exit 1; fi
echo "Android journey passed: $(ls "$OUT" | tr '\n' ' ')"
