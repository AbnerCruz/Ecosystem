#!/usr/bin/env bash
# Gera release-manifest.json, release-notes.md e SHA256SUMS.txt para um APK.
# Uso: tools/make-release-files.sh <apk> <versao-da-tag> <canal> <sha-commit> <pasta-de-saida>
set -euo pipefail
apk="$1"; tag="$2"; channel="$3"; sha="$4"; out="$5"
mkdir -p "$out"
name="Lunet-${tag}-arm64.apk"
cp "$apk" "$out/$name"
size=$(stat -c%s "$out/$name")
hash=$(sha256sum "$out/$name" | cut -d' ' -f1)
built=$(date -u +%Y-%m-%dT%H:%M:%SZ)

# Notas: seção mais recente do CHANGELOG + commits desde a release anterior (se houver).
notes="$out/release-notes.md"
{
  echo "# Lunet ${tag} (${channel})"
  echo
  echo "Build automático do commit \`${sha:0:8}\`. **Não** passou por teste em aparelho físico: veja o ROADMAP."
  echo
  awk '/^## /{n++} n==1' CHANGELOG.md
} > "$notes"

cat > "$out/release-manifest.json" <<JSON
{
  "product": "Lunet",
  "version": "${tag}",
  "channel": "${channel}",
  "commit": "${sha}",
  "builtAt": "${built}",
  "assets": [
    { "name": "${name}", "abi": "arm64-v8a", "minSdk": 29, "size": ${size}, "sha256": "${hash}" }
  ]
}
JSON
python3 - "$out/release-notes.md" "$tag" "$channel" "$sha" "$built" > "$out/release-notes.json" <<'PY'
import json, sys
path, tag, channel, sha, built = sys.argv[1:6]
print(json.dumps({"version": tag, "channel": channel, "commit": sha, "builtAt": built,
                  "notes": open(path, encoding="utf-8").read()}, ensure_ascii=False, indent=2))
PY
(cd "$out" && sha256sum "$name" release-manifest.json release-notes.md release-notes.json > SHA256SUMS.txt)
