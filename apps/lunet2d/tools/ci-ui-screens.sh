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
    case "$name" in
      01a-home|01b-home-resume|01c-home-missing|01d-first-use|01e-appearance|03c-ai-compact|05a-studios-global|05b-sprite-global|05c-music-global|14-f4-editor)
        # A PNG of the Android home screen is not evidence of a working Studio.
        pid="$(adb shell pidof "$package" | tr -d '\r' || true)"
        test -n "$pid" || { echo "Lunet application exited before screenshot: $name"; exit 1; }
        ;;
    esac
    adb shell screencap -p "/sdcard/$name.png"
    adb pull "/sdcard/$name.png" "$out/$name.png" >/dev/null
}

# Warm-up: first start extracts assemblies and JITs Roslyn; not captured.
adb shell am start -W -n "$package/$activity" --es lunet.demo projects >/dev/null
sleep 25

# Portrait phone
shoot 01-projects projects 10
shoot 01a-home home 10
shoot 01b-home-resume home-resume 10
shoot 01c-home-missing home-missing 10
shoot 01d-first-use first-use 5
shoot 01e-appearance appearance 5
shoot 02-workspace workspace 12
shoot 03-ai ai 12
shoot 03b-ai-empty ai-empty 12
# F1: non-modal cowork; code stays visible behind the compact pop-up.
shoot 03c-ai-compact ai-compact 12
shoot 04-palette palette 12
shoot 05-studio studio 12
# F1: real launcher + two editors opened without selecting or creating a game.
shoot 05a-studios-global studios-global 12
shoot 05b-sprite-global sprite-global 12
shoot 05c-music-global music-global 12
shoot 06-notes notes 12
shoot 07-note note 12
shoot 08-sprite sprite 12
shoot 09-ai-settings ai-settings 12
shoot 10-problems problems 12
shoot 10a-learn learn 12
shoot 11-files files 12
shoot 12-run run 25
shoot 13-image image 12

# F4: run the native editor with an actual IME and a Roslyn-resolved call.
adb shell settings put secure show_ime_with_hard_keyboard 1
shoot 14-f4-editor editor-f4 13
adb shell uiautomator dump /sdcard/lunet-f4.xml >/dev/null
adb pull /sdcard/lunet-f4.xml "$out/f4-editor.xml" >/dev/null
grep -q "Desfazer" "$out/f4-editor.xml"
grep -q "Refazer" "$out/f4-editor.xml"
grep -q "Parâmetro ativo: texto" "$out/f4-editor.xml"
adb shell input keyevent KEYCODE_BACK
sleep 2
adb shell uiautomator dump /sdcard/lunet-f4.xml >/dev/null
adb pull /sdcard/lunet-f4.xml "$out/f4-closed-ime.xml" >/dev/null
grep -q "Aba Game.cs" "$out/f4-closed-ime.xml"

# F5: a fresh offline manual and a section opened from the real Aprender workspace.
shoot 15-f5-manual manual-f5 8
adb shell uiautomator dump /sdcard/lunet-f5.xml >/dev/null
adb pull /sdcard/lunet-f5.xml "$out/f5-manual.xml" >/dev/null
for category in "Tutoriais" "Tarefas" "Referência de API" "Conceitos"; do
    grep -q "$category" "$out/f5-manual.xml"
done
shoot 16-f5-mission-doc learn-f5-doc 9
adb shell uiautomator dump /sdcard/lunet-f5.xml >/dev/null
adb pull /sdcard/lunet-f5.xml "$out/f5-mission-doc.xml" >/dev/null
grep -q "Início do toque" "$out/f5-mission-doc.xml"
adb shell input keyevent KEYCODE_BACK
sleep 2
adb shell uiautomator dump /sdcard/lunet-f5.xml >/dev/null
adb pull /sdcard/lunet-f5.xml "$out/f5-return.xml" >/dev/null
grep -q "Aba Game.cs" "$out/f5-return.xml"

# F6: global editors without a project and a native map inside Notes.
shoot 17-f6-mindmap mindmap 8
adb shell uiautomator dump /sdcard/lunet-f6.xml >/dev/null
adb pull /sdcard/lunet-f6.xml "$out/f6-map.xml" >/dev/null
grep -q "Planejar o jogo" "$out/f6-map.xml"
grep -q "Mapa mental" "$out/f6-map.xml"
shoot 18-f6-tile tile-global 8
adb shell uiautomator dump /sdcard/lunet-f6.xml >/dev/null
adb pull /sdcard/lunet-f6.xml "$out/f6-tile.xml" >/dev/null
grep -qi "tile" "$out/f6-tile.xml"
shoot 19-f6-data data-global 8
adb shell uiautomator dump /sdcard/lunet-f6.xml >/dev/null
adb pull /sdcard/lunet-f6.xml "$out/f6-data.xml" >/dev/null
grep -qi "tabela" "$out/f6-data.xml"

# These screens must exist and contain PNG bytes from the Android emulator.
for screen in 01a-home 01b-home-resume 01c-home-missing 03c-ai-compact 05a-studios-global 05b-sprite-global 05c-music-global; do
    test -s "$out/$screen.png" || { echo "F1 screen missing: $screen"; exit 1; }
done

adb logcat -d > "$out/logcat.txt" || true
grep -E "FATAL EXCEPTION|AndroidRuntime" -A20 "$out/logcat.txt" | head -80 || true
echo "Capturas em $out"
