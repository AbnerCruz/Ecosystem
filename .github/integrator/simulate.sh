#!/usr/bin/env bash
# Teste ponta a ponta da cola do integrador (integrate.sh) sem tocar no GitHub (ADR-0015): copia a árvore atual para um repositório
# temporário com um 'origin' bare local, simula PRs em refs/pull/N/head e substitui o gh por um registrador. Os portões e o plano
# (C#) já são cobertos pelo self-test; aqui se prova o efeito no git: o que chega à main, quando, e que nunca há force.
#
#   bash .github/integrator/simulate.sh      (a partir da raiz do repositório; requer git, python3 e .NET SDK 10)
set -euo pipefail

ROOT="$(git rev-parse --show-toplevel)"
T="$(mktemp -d)"; trap 'rm -rf "$T"' EXIT
fails=0
check() { if eval "$2"; then echo "PASS  $1"; else echo "FAIL  $1"; fails=$((fails + 1)); fi; }

git init -q --bare "$T/origin.git"
git clone -q "$T/origin.git" "$T/trusted" 2>/dev/null
cd "$T/trusted"
tar --exclude=./.git --exclude=node_modules --exclude=./site/data -C "$ROOT" -cf - . | tar -xf -
G() { git -c user.name=sim -c user.email=sim@example.invalid "$@"; }
G add -A >/dev/null; G commit -qm "main com integrador"; git push -q origin HEAD:refs/heads/main
MAIN="$(git rev-parse HEAD)"

handoff() { # handoff <id>: handoff mínimo em 'review' (portão NN-008)
  printf '{"message_id": "%s", "state": "review"}\n' "$1" > "docs/governance/handoffs/$1.json"
}
mkdir -p "$T/bin"
cat > "$T/bin/gh" <<'GH'
#!/usr/bin/env bash
echo "gh $*" >> "$GHLOG"
case "$*" in "pr view"*) echo "PR simulado" ;; esac
exit 0
GH
chmod +x "$T/bin/gh"
export PATH="$T/bin:$PATH" GHLOG="$T/gh.log" GITHUB_REPOSITORY=sim/sim RUN_URL=https://example.invalid/run DEFAULT_BRANCH=main
O="$T/out.txt"
prep() { : > "$O"; env GITHUB_OUTPUT="$O" "$@" bash .github/integrator/integrate.sh prepare >/dev/null 2>&1; }
fin() { : > "$GHLOG"; env GITHUB_OUTPUT=/dev/null "$@" bash .github/integrator/integrate.sh finish >/dev/null 2>&1; }
out() { sed -n "s/^$1=//p" "$O"; }
main_is() { [ "$(git --git-dir="$T/origin.git" rev-parse main)" = "$1" ]; }
in_main() { git --git-dir="$T/origin.git" merge-base --is-ancestor "$1" main; }
logged() { grep -q -- "$1" "$GHLOG"; }

# Três PRs a partir da mesma main: 41 só Ecosystem; 42 Urbe; 43 conflita com o 41.
git checkout -q -b a "$MAIN"; echo "nota A" >> docs/governance/README.md; handoff HO-sim-a; G add -A; G commit -qm a; A="$(git rev-parse HEAD)"; git push -q origin HEAD:refs/pull/41/head
git checkout -q -b b "$MAIN"; echo "// B" >> apps/urbe/src/app.js; handoff HO-sim-b; G add -A; G commit -qm b; B="$(git rev-parse HEAD)"; git push -q origin HEAD:refs/pull/42/head
git checkout -q -b c "$MAIN"; echo "nota C" >> docs/governance/README.md; handoff HO-sim-c; G add -A; G commit -qm c; C="$(git rev-parse HEAD)"; git push -q origin HEAD:refs/pull/43/head
git checkout -q --detach "$MAIN"
floor="$(python3 -c 'import json;print(json.load(open("ecosystem.json"))["ecosystem"]["mergePolicy"])')"

prep PR=41 HEAD_SHA="$A" MAIN="$MAIN"; CA="$(out combined)"; POL="$(out policy)"
check "estado combinado montado e publicado em integration/pr-41" '[ -n "$CA" ] && [ "$(git --git-dir="$T/origin.git" rev-parse refs/heads/integration/pr-41)" = "$CA" ]'
check "PR só do Ecosystem: política = piso do Ecosystem ($floor)" '[ "$POL" = "$floor" ]'
fin ACTION=evaluate PR=41 HEAD_SHA="$A" MAIN="$MAIN" COMBINED="$CA" POLICY=owner-authorization POLICY_REASON=piso AUTHORIZED=false MORE=false R_CONSISTENCY=success R_URBE=skipped R_LUNET2D=skipped
check "verde sem autorização: 'pronto', label pronto-para-integrar, main intocada" 'logged "description=pronto main=${MAIN:0:12}" && logged "add-label pronto-para-integrar" && main_is "$MAIN"'
fin ACTION=land PR=41 HEAD_SHA="$A" MAIN="$MAIN" POLICY=owner-authorization AUTHORIZED=true MORE=false
check "autorizado (land): a main vira exatamente o commit testado, com o head do PR" 'main_is "$CA" && in_main "$A" && logged "description=integrado"'
check "depois de integrar: pages, consistency e a fila são redisparados" 'logged "workflow run pages.yml" && logged "workflow run consistency.yml" && logged "workflow run integrate.yml"'

git fetch -q origin main; M2="$(git rev-parse origin/main)"; git checkout -q --detach "$M2"
prep PR=43 HEAD_SHA="$C" MAIN="$M2"
check "conflito com a main atual: bloqueado, arquivo apontado, nada publicado" '[ "$(out action)" = blocked ] && [ "$(out conflicts)" = docs/governance/README.md ] && main_is "$M2"'
fin ACTION=blocked OUTCOME=conflito CONFLICTS=docs/governance/README.md PR=43 HEAD_SHA="$C" MAIN="$M2" MORE=false
check "conflito volta ao autor: label precisa-reconciliar" 'logged "add-label precisa-reconciliar"'

prep PR=42 HEAD_SHA="$B" MAIN="$M2"; CB="$(out combined)"
check "PR do Urbe: CI do Urbe no estado combinado e autorização exigida" '[ "$(out products)" = "[\"urbe\"]" ] && [ "$(out policy)" = owner-authorization ]'
fin ACTION=evaluate PR=42 HEAD_SHA="$B" MAIN="$M2" COMBINED="$CB" POLICY=owner-authorization AUTHORIZED=true MORE=false R_CONSISTENCY=success R_URBE=failure R_LUNET2D=skipped
check "check vermelho no estado combinado: 'falhou', main intocada" 'logged "description=falhou" && main_is "$M2"'

echo paralelo > PARALELO.txt; G add -A; G commit -qm paralelo; git push -q origin HEAD:refs/heads/main; git checkout -q --detach "$M2"
fin ACTION=evaluate PR=42 HEAD_SHA="$B" MAIN="$M2" COMBINED="$CB" POLICY=owner-authorization AUTHORIZED=true MORE=false R_CONSISTENCY=success R_URBE=success R_LUNET2D=skipped
check "a main mudou durante o teste: o teste velho não entra; reavaliação" '! in_main "$B" && logged "description=testando"'

git fetch -q origin main; M3="$(git rev-parse origin/main)"; git checkout -q --detach "$M3"
prep PR=42 HEAD_SHA="$B" MAIN="$M3"; CB2="$(out combined)"
fin ACTION=evaluate PR=42 HEAD_SHA="$B" MAIN="$M3" COMBINED="$CB2" POLICY=owner-authorization AUTHORIZED=true MORE=false R_CONSISTENCY=success R_URBE=success R_LUNET2D=skipped
check "reavaliado contra a main nova: integra o novo combinado, com a mudança paralela e o PR" 'main_is "$CB2" && in_main "$M3" && in_main "$B"'
check "a main só avançou por fast-forward (nenhum force)" '( for x in "$MAIN" "$CA" "$M2" "$M3" "$CB2"; do in_main "$x" || exit 1; done )'

echo
if [ "$fails" -eq 0 ]; then echo "Simulação do integrador: todos os cenários passaram."; else echo "Simulação do integrador: $fails falha(s)."; exit 1; fi
