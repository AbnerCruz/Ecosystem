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
# the screen as the accessibility tree sees it (Avalonia exposes its texts and names)
screen() { adb shell uiautomator dump /sdcard/ui.xml > /dev/null 2>&1; adb exec-out cat /sdcard/ui.xml; }
# the emulator's own launcher sometimes stops answering right after boot and its system dialog covers the app (the app
# logs no error). The check retries for a while; a dialog about another app is closed ("Close app" closes that app, the
# launcher, never the Urbe), one about the Urbe is a real failure.
expect() {
  local i
  for i in $(seq 1 20); do
    screen > "$OUT/$2.xml"
    grep -q "$1" "$OUT/$2.xml" && return 0
    if grep -q "isn't responding" "$OUT/$2.xml"; then
      grep -q "Urbe[^\"]*isn't responding" "$OUT/$2.xml" && break
      echo "a system dialog of another app covers the Urbe (attempt $i): $(grep -o 'text="[^"]*responding"' "$OUT/$2.xml" | head -1)"
      tap_text "Close app" || tap_text "Wait"
    fi
    sleep 2
  done
  echo "::error::'$1' not on screen at $2"; shot "$2"; adb logcat -d -v threadtime > "$OUT/logcat.txt"; exit 1
}
tap_text() {
  local b; b=$(screen | grep -o "text=\"$1\"[^>]*bounds=\"[^\"]*\"" | head -1 | grep -o 'bounds="[^"]*"' | tr -dc '0-9,[]' | tr '[]' ' ')
  [ -n "$b" ] && set -- $(echo "$b" | tr ',' ' ') && adb shell input tap $(( ($1 + $3) / 2 )) $(( ($2 + $4) / 2 ))
}
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
expect "Roteiro" 01-notas
shot 01-notas
tap_text "Entendi"   # the first-use tip
sleep 1

# 2. the video publication opens in the Estúdio (not as JSON) and plays — recorded
adb shell screenrecord --time-limit 24 /sdcard/urbe-video.mp4 &
REC=$!
sleep 1
# its own action: an intent equal to the task's MAIN/LAUNCHER (extras do not count) only brings the task forward
ACTIVITY=$(adb shell cmd package resolve-activity --brief -c android.intent.category.LAUNCHER "$PKG" | tail -1 | tr -d '\r')
adb shell am start --activity-single-top -n "$ACTIVITY" -a app.urbe.e2e.OPEN \
  --es urbe.open "Publicações/Demo.publication.json" --ez urbe.play true
# screenshots first: an accessibility dump takes seconds and the video lasts 12
sleep 3
shot 02-estudio
adb shell input swipe 540 1700 540 900 300
sleep 1
shot 03-video-tocando
sleep 4
shot 04-video-cena-2
expect "Prévia do vídeo" 04-estudio
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
