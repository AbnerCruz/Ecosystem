#!/usr/bin/env bash
# Build glue only. One packaging implementation for PR preview and governed CI.
set -euo pipefail
: "${ANDROID_HOME:?Android SDK required}"
: "${JAVA_HOME:?JDK required}"
(cd tools && sha256sum -c ecosystem-ai-preview.keystore.sha256)
dotnet workload install android

# GitHub-hosted runners expose ANDROID_HOME but do not consistently add the
# command-line tools directory to PATH. Resolve the SDK manager from the SDK
# itself so the same build script works in the Product CI and preview jobs.
SDKMANAGER="$(command -v sdkmanager || true)"
if [[ -z "$SDKMANAGER" ]]; then
  for candidate in \
    "$ANDROID_HOME/cmdline-tools/latest/bin/sdkmanager" \
    "$ANDROID_HOME"/cmdline-tools/*/bin/sdkmanager \
    "$ANDROID_HOME/tools/bin/sdkmanager"; do
    if [[ -x "$candidate" ]]; then
      SDKMANAGER="$candidate"
      break
    fi
  done
fi
if [[ -z "$SDKMANAGER" ]]; then
  echo "Android SDK manager not found under ANDROID_HOME=$ANDROID_HOME" >&2
  exit 127
fi
"$SDKMANAGER" --sdk_root="$ANDROID_HOME" "platforms;android-36" "build-tools;36.0.0"
dotnet publish src/EcosystemAi.Android/EcosystemAi.Android.csproj -c Release \
  -p:AndroidSdkDirectory="$ANDROID_HOME" -p:JavaSdkDirectory="$JAVA_HOME" \
  -p:ApplicationDisplayVersion="$(cat VERSION)-preview.${GITHUB_RUN_NUMBER:-local}" \
  -p:AndroidPreviewBuildNumber="$(($(date +%s) / 60))"
mkdir -p android-preview
apk="$(find src/EcosystemAi.Android/bin/Release -name '*-Signed.apk' -print -quit)"
test -n "$apk" && test -s "$apk"
cp "$apk" android-preview/Ecosystem-AI-Android-Preview.apk
cp docs-r4-android-native.md android-preview/README.md
(cd android-preview && sha256sum Ecosystem-AI-Android-Preview.apk > SHA256SUMS.txt)
"$ANDROID_HOME/build-tools/36.0.0/apksigner" verify --verbose android-preview/Ecosystem-AI-Android-Preview.apk
