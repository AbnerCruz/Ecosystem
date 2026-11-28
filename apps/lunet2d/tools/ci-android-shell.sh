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

tap_accessible() {
    local label="$1"
    local id="${2:-}"
    local coords
    for attempt in $(seq 1 8); do
        adb shell uiautomator dump /sdcard/lunet-ui.xml >/dev/null || continue
        if coords=$(adb exec-out cat /sdcard/lunet-ui.xml | python3 -c '
import re
import sys
import xml.etree.ElementTree as ET
for node in ET.parse(sys.stdin).iter("node"):
    if node.get("enabled") == "true" and (sys.argv[1].casefold() in (node.get("text", "").casefold(), node.get("content-desc", "").casefold()) or (sys.argv[2] and node.get("resource-id", "").endswith(sys.argv[2]))):
        x0, y0, x1, y1 = map(int, re.findall(r"\d+", node.attrib["bounds"]))
        print((x0 + x1) // 2, (y0 + y1) // 2)
        break
else:
    sys.exit(1)
' "$label" "$id"); then
            read -r x y <<< "$coords"
            adb shell input tap "$x" "$y"
            return
        fi
    done
    adb exec-out cat /sdcard/lunet-ui.xml > "$out/android-ui-failure.xml" || true
    echo "A ação $label não apareceu na árvore de acessibilidade do Android" >&2
    return 1
}

wait_log 'editor=pronto'
ide_pid=$(adb shell pidof "$package" | tr -d '\r')
test -n "$ide_pid"
tap_accessible Rodar
wait_log 'quadros=[1-9]'
preview_pid=$(adb shell pidof "$package:preview" | tr -d '\r')
test -n "$preview_pid"
test "$preview_pid" != "$ide_pid"
# Preview hang regression: while Update is hung, Back must end that preview and leave the editor in the original process.
wait_log 'LUNET_G6_UPDATE_BLOQUEADO'
adb shell input keyevent KEYCODE_BACK
wait_log 'preview=recuperado'
grep -q 'LunetPreview.*não respondeu ao comando de saída' "$out/android-logcat.txt"
cp "$out/android-logcat.txt" "$out/android-preview-crash-logcat.txt"
test "$(adb shell pidof "$package" | tr -d '\r')" = "$ide_pid"

# Reopen the same game and exercise the Android Back key on the new preview.
adb logcat -c
tap_accessible Rodar
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


# Select an ordinary shared folder through the Android picker, then check the provider files and restart Lunet.
adb logcat -c
adb shell mkdir -p /sdcard/Documents/LunetCi
adb shell am start -W -n "$package/$activity" --ez lunet.saf true >/dev/null
tap_accessible 'USE THIS FOLDER' ':id/action_menu_select'
tap_accessible Allow 'android:id/button1'
wait_log 'saf=gravado rascunho=preparado'
adb shell cat /sdcard/Documents/LunetCi/ProjetoSaf/Game.cs | grep -qF '// lunet-e2e salvamento direto'
cp "$out/android-logcat.txt" "$out/android-saf-logcat.txt"
adb shell am force-stop "$package"
adb logcat -c
adb shell am start -W -n "$package/$activity" --ez lunet.saf.verify true >/dev/null
wait_log 'saf=permissao-persistida rascunho=recuperado'
adb shell cat /sdcard/Documents/LunetCi/ProjetoSaf/Game.cs | grep -qF '// lunet-e2e rascunho'
adb shell am force-stop "$package"

# Existing SAF project, open tabs and real Android taps for cancel/create.
adb logcat -c
adb shell am start -W -n "$package/$activity" --ez lunet.files.ux true >/dev/null
wait_log 'filesux=pronto'
tap_accessible 'Novo arquivo C#' ExplorerNewFile
sleep 1
adb exec-out screencap -p > "$out/android-file-create.png"
tap_accessible Cancelar ExplorerCancelCreate
tap_accessible 'Novo arquivo C#' ExplorerNewFile
tap_accessible 'Nome do novo arquivo ou pasta' ExplorerCreateName
adb shell input text Player
tap_accessible Criar ExplorerConfirmCreate
sleep 3
adb shell input keyevent KEYCODE_BACK
adb shell uiautomator dump /sdcard/lunet-files.xml >/dev/null
adb exec-out cat /sdcard/lunet-files.xml > "$out/android-file-tabs.xml"
grep -q 'Player.cs' "$out/android-file-tabs.xml"
adb shell cat /sdcard/Documents/LunetCi/ProjetoSaf/ExistingTab.cs | grep -qF '// edição do emulador'
adb shell cat /sdcard/Documents/LunetCi/ProjetoSaf/Player.cs | grep -qF 'class Player'
adb exec-out screencap -p > "$out/android-file-tabs.png"
adb shell pidof "$package" | grep -q '[0-9]'
adb shell am force-stop "$package"

# Long chat, local provider, switching games and cancelling while the first game still owns the run.
adb logcat -c
adb shell am start -W -n "$package/$activity" --ez lunet.ai.stability true >/dev/null
wait_log 'ai=preservada turnos=218'
cp "$out/android-logcat.txt" "$out/android-ai-logcat.txt"
adb shell dumpsys meminfo "$package" > "$out/android-ai-memory.txt"
adb exec-out screencap -p > "$out/android-ai-stability.png"
adb shell pidof "$package" | grep -q '[0-9]'
adb shell am force-stop "$package"

# Open the documented project from the granted folder and capture its isolated Preview.
demo=/sdcard/Documents/LunetCi/random-coins
sample=apps/lunet2d/samples/random-coins
adb shell mkdir -p "$demo/Project"
adb push "$sample/Game.cs" "$demo/Game.cs"
adb push "$sample/lunet.json" "$demo/lunet.json"
adb push "$sample/Project/Imports.cs" "$demo/Project/Imports.cs"
adb logcat -c
adb shell am start -W -n "$package/$activity" --ez lunet.random.demo true >/dev/null
wait_log 'random=editor'
wait_log 'quadros=[1-9]'
adb exec-out screencap -p > "$out/android-random-preview.png"
adb shell input keyevent KEYCODE_BACK
sleep 1
adb exec-out screencap -p > "$out/android-random-editor.png"
adb shell pidof "$package" | grep -q '[0-9]'
adb shell am force-stop "$package"
