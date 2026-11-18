#!/usr/bin/env bash
# Android UI journey on the CI emulator (urbe-checks.yml → android-runtime-smoke), run from the repository root.
# The emulator build (URBE_EMULATOR) opens the example vault pushed into the app's own folder and accepts
# "--es urbe.open <file> --ez urbe.play true". It leaves screenshots, a screen recording and the logcat in $OUT.
# Emulator evidence is not DEVICE validation (NN-017): the moto g32 test stays with the owner.
set -u
PKG=app.urbe.nativepreview
OUT=apps/urbe/csharp/artifacts/android-smoke
DATA=/sdcard/Android/data/$PKG/files
mkdir -p "$OUT"
shot() { adb exec-out screencap -p > "$OUT/$1.png"; }
alive() { adb shell pidof "$PKG" > /dev/null || { echo "::error::$PKG is not running after: $1"; adb logcat -d -v threadtime > "$OUT/logcat.txt"; exit 1; }; }

adb install -r "$(find apps/urbe/csharp/artifacts/emulator -name '*-Signed.apk' | head -1)" || exit 1
adb shell mkdir -p "$DATA"
adb push apps/urbe/csharp/tests/fixtures/e2e-vault "$DATA/e2e-vault" > /dev/null || exit 1
adb logcat -c

# 1. opening: the example vault's notes
adb shell monkey -p "$PKG" -c android.intent.category.LAUNCHER 1 > /dev/null
sleep 12
adb logcat -d -v threadtime > "$OUT/startup-logcat.txt"
alive "launch"
shot 01-notas

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
