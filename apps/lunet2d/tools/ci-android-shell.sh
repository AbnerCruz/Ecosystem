#!/usr/bin/env bash
# ADR 0013: Lunet (entry = the Avalonia shell) on the CI emulator (Android 13, x86_64). Runs in ONE shell, since
# android-emulator-runner runs each 'script:' line separately. Emulator evidence, not a device test (ADD-0035).
set -euo pipefail

out=apps/lunet2d/dist/shell
apk="$out/lunet-x64.apk"
package=io.lunet.studio
test -s "$apk"
aapt="$(find "$ANDROID_HOME/build-tools" -name aapt | sort -V | tail -n1)"
activity="$("$aapt" dump badging "$apk" | sed -n "s/^launchable-activity: name='\([^']*\)'.*/\1/p" | head -n1)"
test -n "$activity"

adb install -r "$apk"
adb shell pm path "$package" | grep -q '^package:'

# Cold start: TotalTime from the activity manager ("time to initial display").
adb logcat -c
adb shell am start -W -n "$package/$activity" | tee "$out/android-launch.txt"
grep -q 'Status: ok' "$out/android-launch.txt"
sleep 6
adb shell pidof "$package" | grep -q '[0-9]' || { adb logcat -d | tail -n 150; echo 'Lunet fechou ao abrir'; exit 1; }
adb shell screencap -p /sdcard/shell-home.png
adb pull /sdcard/shell-home.png "$out/android-shell-home.png"
adb shell am force-stop "$package"

# Journey: create a game, compile with Roslyn and run it inside the shell (hook of the emulator build).
adb logcat -c
adb shell am start -W -n "$package/$activity" --ez lunet.e2e true > /dev/null
ok=false
for attempt in $(seq 1 30); do
    adb logcat -d > "$out/android-logcat.txt"
    if grep -q 'lunet-e2e.*estado=Running' "$out/android-logcat.txt"; then ok=true; break; fi
    if grep -q 'lunet-e2e.*\(falhou\|estado=\)' "$out/android-logcat.txt"; then break; fi
    sleep 2
done
grep 'lunet-e2e' "$out/android-logcat.txt" || true
adb shell screencap -p /sdcard/shell-game.png
adb pull /sdcard/shell-game.png "$out/android-shell-game.png"
if [[ "$ok" != true ]]; then
    tail -n 150 "$out/android-logcat.txt"
    echo 'O jogo não rodou dentro da casca no emulador'
    exit 1
fi
grep -q 'quadros=[1-9]' "$out/android-logcat.txt" || { echo 'Nenhum quadro renderizado'; exit 1; }

# The IDE keyboard (owner's order, PR #1040): visible, the system IME off on the editor, keys typed into the code.
for attempt in $(seq 1 15); do
    adb logcat -d > "$out/android-logcat.txt"
    grep -q 'lunet-e2e.*teclado=' "$out/android-logcat.txt" && break
    sleep 2
done
grep 'lunet-e2e.*teclado=' "$out/android-logcat.txt" || { echo 'O teclado Lunet não respondeu'; exit 1; }
adb shell screencap -p /sdcard/shell-keyboard.png
adb pull /sdcard/shell-keyboard.png "$out/android-shell-keyboard.png"
adb shell dumpsys input_method | grep -E 'mInputShown|mIsInputViewShown' > "$out/android-ime.txt" || true
grep -q 'lunet-e2e.*teclado=visivel ime=off texto=// ola' "$out/android-logcat.txt" || { echo 'Teclado Lunet: estado inesperado'; exit 1; }
adb shell am force-stop "$package"
echo "Lunet: instalado, aberto ($(grep TotalTime "$out/android-launch.txt" | tr -d '\r')), jogo criado, compilado e rodando no Android 13 (emulador)."
