#!/usr/bin/env bash
# Exercise the same Release app on an Android 13 x86_64 emulator.
# The distributed arm64 APK remains a separate build and requires DEVICE validation.
set -euo pipefail
: "${ANDROID_HOME:?Android SDK required}"
sdkmanager="${1:?SDK manager path required}"
test -s android-preview/Ecosystem-AI-Android-Preview.apk
test -c /dev/kvm || { echo "Android launch test requires KVM" >&2; exit 1; }
if [[ ! -w /dev/kvm ]]; then sudo chmod a+rw /dev/kvm; fi
"$sdkmanager" --sdk_root="$ANDROID_HOME" emulator platform-tools 'system-images;android-33;google_apis;x86_64'
scratch="$(mktemp -d "${RUNNER_TEMP:-/tmp}/ecoai-launch.XXXXXX")"
dotnet publish src/EcosystemAi.Android/EcosystemAi.Android.csproj -c Release \
  -p:RuntimeIdentifier=android-x64 -p:AndroidSdkDirectory="$ANDROID_HOME" \
  -p:JavaSdkDirectory="$JAVA_HOME" -o "$scratch/apk"
apk="$(find "$scratch/apk" -name '*-Signed.apk' -print -quit)"
test -n "$apk" && test -s "$apk"
badging="$("$ANDROID_HOME/build-tools/36.0.0/aapt2" dump badging "$apk")"
printf '%s\n' "$badging"
package="$(sed -n "s/^package: name='\([^']*\)'.*/\1/p" <<< "$badging")"
activity="$(sed -n "s/^launchable-activity: name='\([^']*\)'.*/\1/p" <<< "$badging")"
[[ "$package" == io.ecosystem.ai.preview && -n "$activity" ]]
export ANDROID_AVD_HOME="$scratch/avds"
mkdir -p "$ANDROID_AVD_HOME"
printf 'no\n' | "$(dirname "$sdkmanager")/avdmanager" create avd --force \
  --name ecoai-launch --package 'system-images;android-33;google_apis;x86_64' --path "$scratch/device"
adb="$ANDROID_HOME/platform-tools/adb"
serial=emulator-5580
passed=false
cleanup() {
  "$adb" -s "$serial" logcat -b all -d > android-preview/android-launch-logcat.txt 2>&1 || true
  if [[ "$passed" != true ]]; then
    echo 'Android launch test failed. Crash buffer:' >&2
    "$adb" -s "$serial" logcat -b crash -d >&2 || true
    grep -E 'AndroidRuntime|FATAL|mono|monodroid|Ecosystem|ecosystem.ai|SIGABRT' \
      android-preview/android-launch-logcat.txt | tail -150 >&2 || true
    if [[ -f android-preview/android-launch-ui.xml ]]; then cat android-preview/android-launch-ui.xml >&2; fi
    "$adb" -s "$serial" shell dumpsys package io.ecosystem.ai.preview >&2 || true
    tail -30 "$scratch/emulator.log" >&2 || true
  fi
  "$adb" -s "$serial" emu kill >/dev/null 2>&1 || true
}
trap cleanup EXIT
"$ANDROID_HOME/emulator/emulator" -avd ecoai-launch -port 5580 -no-window \
  -no-audio -no-boot-anim -no-snapshot -gpu swiftshader_indirect \
  -memory 2048 -cores 2 > "$scratch/emulator.log" 2>&1 &
timeout 180 "$adb" -s "$serial" wait-for-device
deadline=$((SECONDS + 180))
until [[ "$("$adb" -s "$serial" shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')" == 1 ]]; do
  ((SECONDS < deadline)) || { echo "Android emulator did not boot" >&2; exit 1; }
  sleep 2
done
# Refuse to install on a physical device, even if a serial is accidentally changed.
[[ "$("$adb" -s "$serial" shell getprop ro.kernel.qemu | tr -d '\r')" == 1 ]]
"$adb" -s "$serial" install "$apk"
"$adb" -s "$serial" logcat -c
"$adb" -s "$serial" shell input keyevent 82
"$adb" -s "$serial" shell am start -W -a android.intent.action.MAIN \
  -c android.intent.category.LAUNCHER -n "$package/$activity" | tee "$scratch/launch.txt"
grep -q 'Status: ok' "$scratch/launch.txt"
sleep 12
test -n "$("$adb" -s "$serial" shell pidof io.ecosystem.ai.preview)"
timeout 30 "$adb" -s "$serial" shell uiautomator dump /data/local/tmp/ecoai-launch.xml
"$adb" -s "$serial" pull /data/local/tmp/ecoai-launch.xml android-preview/android-launch-ui.xml
grep -q 'Comece com um projeto' android-preview/android-launch-ui.xml
passed=true
echo 'Android 13 launch passed: Release x86_64 app stayed alive and rendered the project home screen; arm64 DEVICE validation remains separate.'
