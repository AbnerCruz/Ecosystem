#!/usr/bin/env python3
"""Fail native Android release if the original 1.8.4-beta bitmaps were not bundled.

Checks the *signed APK*, not build intermediates, against hashes generated
directly by the original pixel-art.js in a headless build-time Chromium.
"""
import hashlib
import json
import re
import struct
import sys
import zipfile
from pathlib import Path

PREFIX = "assets/wwwroot/_content/Urbe.UI/world/v184/"
ORIGINAL_SOURCE_SHA256 = "77ceb442a67fdb21adb1c9c5b376bf8918ca19691ddcb39f8c0aea5f3ba771de"
EXPECTED_ASSETS = 206
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"


def check(apk_path: Path, sums_path: Path) -> None:
    if not apk_path.is_file() or not sums_path.is_file():
        raise RuntimeError("Signed APK or SHA256SUMS.txt not found")
    apk_sha = hashlib.sha256(apk_path.read_bytes()).hexdigest()
    expected_line = f"{apk_sha}  {apk_path.name}"
    lines = sums_path.read_text(encoding="utf-8").splitlines()
    if lines != [expected_line]:
        raise RuntimeError("SHA256SUMS does not match the signed APK bytes")

    with zipfile.ZipFile(apk_path) as apk:
        index = set(apk.namelist())
        manifest_key = PREFIX + "art-manifest.json"
        if manifest_key not in index:
            raise RuntimeError("Original Urbe art manifest missing from the signed APK")
        metadata = json.loads(apk.read(manifest_key))
        if metadata.get("source") != "Urbe 1.8.4-beta":
            raise RuntimeError("Wrong version of art embedded")
        if metadata.get("source_sha256") != ORIGINAL_SOURCE_SHA256:
            raise RuntimeError("Original source fingerprint differs")
        if metadata.get("tile_size_px") != 16:
            raise RuntimeError("Original 16px tiles were altered")

        assets = metadata.get("assets")
        if not isinstance(assets, list) or len(assets) != EXPECTED_ASSETS:
            raise RuntimeError(f"Expected {EXPECTED_ASSETS} original bitmaps, got {len(assets) if isinstance(assets, list) else 'none'}")
        expected_names = set()
        for asset in assets:
            name = asset["name"]
            if not re.fullmatch(r"[a-z0-9-]+\.png", name) or name in expected_names:
                raise RuntimeError(f"Bad or duplicate original asset name: {name}")
            expected_names.add(name)
            key = PREFIX + name
            if key not in index:
                raise RuntimeError(f"Original sprite missing from signed APK: {name}")
            image = apk.read(key)
            if hashlib.sha256(image).hexdigest() != asset["sha256"]:
                raise RuntimeError(f"Original sprite pixel/PNG bytes changed: {name}")
            if not image.startswith(PNG_SIGNATURE) or image[12:16] != b"IHDR":
                raise RuntimeError(f"Invalid original PNG sprite: {name}")
            width, height = struct.unpack(">II", image[16:24])
            size = ((48, 56) if name.startswith("building-") else
                    (24, 32) if name.startswith("tree-") else (16, 16))
            if (width, height) != size:
                raise RuntimeError(f"Unexpected original sprite size {name}: {width}x{height}")

        actual_names = {key[len(PREFIX):] for key in index
                        if key.startswith(PREFIX) and key.endswith(".png")}
        if actual_names != expected_names:
            raise RuntimeError("Unmanifested PNGs present in signed Android package")

    print(f"Urbe 1.8.4-beta art: {EXPECTED_ASSETS} original PNGs and signed APK SHA-256 verified: {apk_sha}")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit("Usage: verify-legacy-v184-apk.py APK SHA256SUMS.txt")
    try:
        check(Path(sys.argv[1]), Path(sys.argv[2]))
    except (OSError, KeyError, ValueError, RuntimeError, zipfile.BadZipFile) as exc:
        raise SystemExit(f"ART VERIFY FAILED: {exc}")
