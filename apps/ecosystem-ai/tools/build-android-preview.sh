#!/usr/bin/env bash
# Build glue only. One packaging implementation for PR preview and governed CI.
set -euo pipefail
: "${ANDROID_HOME:?Android SDK required}"
: "${JAVA_HOME:?JDK required}"
(cd tools && sha256sum -c ecosystem-ai-preview.keystore.sha256)
dotnet workload install android
sdkmanager "platforms;android-36" "build-tools;36.0.0"
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
