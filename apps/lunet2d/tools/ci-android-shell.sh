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

# The hook seeds the project and opens the keyboard. Android itself delivers the tap and Back.
adb logcat -c
trap 'adb logcat -d > "$out/android-final-logcat.txt" || true' EXIT
adb shell am start -W -n "$package/$activity" --ez lunet.e2e true > /dev/null

wait_log() {
    local pattern="$1"
    for attempt in $(seq 1 30); do
        adb logcat -d > "$out/android-logcat.txt"
        if grep -q "lunet-e2e.*$pattern" "$out/android-logcat.txt"; then return; fi
        if grep -q 'lunet-e2e.*falhou' "$out/android-logcat.txt"; then break; fi
        sleep 2
    done
    grep -E 'lunet-e2e|LunetPreview|ANR in io.lunet.studio|AndroidRuntime.*FATAL EXCEPTION' "$out/android-logcat.txt" | tail -40 || true
    echo "O log esperado não chegou: $pattern" >&2
    return 1
}

tap_run() {
    local coords
    for attempt in $(seq 1 3); do
        adb shell uiautomator dump /sdcard/lunet-ui.xml >/dev/null || continue
        if coords=$(adb exec-out cat /sdcard/lunet-ui.xml | python3 -c '
import re
import sys
import xml.etree.ElementTree as ET
for node in ET.parse(sys.stdin).iter("node"):
    if "Rodar" in (node.get("text"), node.get("content-desc")) and node.get("enabled") == "true":
        x0, y0, x1, y1 = map(int, re.findall(r"\d+", node.attrib["bounds"]))
        print((x0 + x1) // 2, (y0 + y1) // 2)
        break
else:
    sys.exit(1)
'); then
            read -r x y <<< "$coords"
            adb shell input tap "$x" "$y"
            return
        fi
    done
    echo 'O botão Rodar não apareceu na árvore de acessibilidade do Android' >&2
    return 1
}

wait_log 'editor=pronto'
ide_pid=$(adb shell pidof "$package" | tr -d '\r')
test -n "$ide_pid"
tap_run
wait_log 'quadros=[1-9]'
preview_pid=$(adb shell pidof "$package:preview" | tr -d '\r')
test -n "$preview_pid"
test "$preview_pid" != "$ide_pid"
# G6: while Update is hung, Back must end that preview and leave the editor in the original process.
wait_log 'LUNET_G6_UPDATE_BLOQUEADO'
adb shell input keyevent KEYCODE_BACK
wait_log 'preview=recuperado'
grep -q 'LunetPreview.*não respondeu ao comando de saída' "$out/android-logcat.txt"
cp "$out/android-logcat.txt" "$out/android-preview-crash-logcat.txt"
test "$(adb shell pidof "$package" | tr -d '\r')" = "$ide_pid"

# Reopen the same game and exercise the Android Back key on the new preview.
adb logcat -c
tap_run
wait_log 'quadros=[1-9]'
adb shell screencap -p /sdcard/shell-game.png
adb pull /sdcard/shell-game.png "$out/android-shell-game.png"
adb shell input keyevent KEYCODE_BACK
wait_log 'preview=cheio retorno=editor'
wait_log 'barra=visivel ime=on texto={'
test "$(adb shell pidof "$package" | tr -d '\r')" = "$ide_pid"
adb shell screencap -p /sdcard/shell-keyboard.png
adb pull /sdcard/shell-keyboard.png "$out/android-shell-keyboard.png"
adb shell dumpsys input_method | grep -E 'mInputShown|mIsInputViewShown' > "$out/android-ime.txt" || true
grep 'lunet-e2e' "$out/android-preview-crash-logcat.txt" "$out/android-logcat.txt"
adb shell am force-stop "$package"
echo "Lunet: Rodar abriu o preview, a IDE sobreviveu à saída do processo e o segundo Rodar voltou ao arquivo com Voltar."
