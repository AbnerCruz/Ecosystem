#!/usr/bin/env bash
# Teste ponta a ponta da cola do integrador (integrate.sh) sem tocar no GitHub (ADR-0015, ADD-0012): copia a árvore atual para um
# repositório temporário com um 'origin' bare local, simula PRs em refs/pull/N/head e substitui o gh por um simulador COM ESTADO
# (head real de cada PR, statuses por commit, eventos de label com o ator). As decisões (fila, criticidade, autorização) são as funções
# C# reais; aqui se prova o efeito no git: o que chega à main, quando, e que nunca há force.
#
#   bash .github/integrator/simulate.sh      (a partir da raiz do repositório; requer git, jq, python3 e .NET SDK 10)
set -euo pipefail

ROOT="$(git rev-parse --show-toplevel)"
T="$(mktemp -d)"; trap 'rm -rf "$T"' EXIT
fails=0
check() { if eval "$2"; then echo "PASS  $1"; else echo "FAIL  $1"; fails=$((fails + 1)); fi; }

# Origin com o histórico REAL (HEAD + tags): o checker confiável confere SHAs dos handoffs e o histórico migrado (exige fetch-depth: 0).
git init -q --bare "$T/origin.git"
# Diagnósticos do git ficam visíveis: falha de bootstrap não é cenário aprovado.
git -C "$ROOT" push -q "$T/origin.git" "HEAD:refs/heads/main" "refs/tags/*:refs/tags/*"
git --git-dir="$T/origin.git" symbolic-ref HEAD refs/heads/main
git clone -q "$T/origin.git" "$T/trusted"
cd "$T/trusted"
G() { git -c user.name=sim -c user.email=sim@example.invalid "$@"; }
# A main simulada = a árvore em teste (inclui o que ainda não foi commitado), por cima do histórico real.
git ls-files -z | xargs -0 rm -f
tar --exclude=./.git --exclude=node_modules --exclude=./site/data -C "$ROOT" -cf - . | tar -xf -
G add -A >/dev/null; G commit -q --allow-empty -m "main com integrador (árvore em teste)"; git push -q origin HEAD:refs/heads/main
MAIN="$(git rev-parse HEAD)"

# --- simulador do gh: estado em $T/gh -------------------------------------------------------------------------------------
mkdir -p "$T/bin" "$T/gh/statuses" "$T/gh/events" "$T/gh/prs" "$T/gh/labels"
echo 0 > "$T/gh/clock"
cat > "$T/bin/gh" <<'GH'
#!/usr/bin/env bash
S="$GHSTATE"; echo "gh $*" >> "$GHLOG"
tick() { local n; n=$(( $(cat "$S/clock") + 1 )); echo "$n" > "$S/clock"; date -u -d "@$((1790000000 + n))" +%Y-%m-%dT%H:%M:%SZ; }
arg() { local k="$1"; shift; while [ $# -gt 0 ]; do case "$1" in "$k="*) echo "${1#*=}"; return ;; esac; shift; done; }
case "$*" in
  "pr view"*) echo "PR simulado" ;;
  "pr edit"*)
    n="$3"; lbl=""; op=""
    for a in "$@"; do [ "$op" = add ] && { lbl="$a"; op=labeled; }; [ "$op" = rm ] && { lbl="$a"; op=unlabeled; }; case "$a" in --add-label) op=add ;; --remove-label) op=rm ;; esac; done
    touch "$S/labels/$n"
    if [ "$op" = labeled ]; then grep -qx "$lbl" "$S/labels/$n" || echo "$lbl" >> "$S/labels/$n"
    elif grep -qx "$lbl" "$S/labels/$n"; then grep -vx "$lbl" "$S/labels/$n" > "$S/labels/$n.tmp" || true; mv "$S/labels/$n.tmp" "$S/labels/$n"
    else exit 0; fi
    printf '{"event":"%s","label":{"name":"%s"},"actor":{"login":"github-actions[bot]"},"created_at":"%s"}\n' "$op" "$lbl" "$(tick)" >> "$S/events/$n.jsonl" ;;
  "workflow run lunet2d-release.yml"*) [ "${FAIL_LUNET_DISPATCH:-false}" != true ] || exit 1 ;;
  "workflow run hub-release.yml"*) [ "${FAIL_HUB_DISPATCH:-false}" != true ] || exit 1 ;;
  "label create"*|"workflow run"*) ;;
  "api -X POST repos/"*"/statuses/"*)
    sha="${4##*/}"; desc="$(arg description "$@")"; st="$(arg state "$@")"
    jq -nc --arg d "$desc" --arg s "$st" --arg t "$(tick)" '{state:$s, description:$d, created_at:$t, creator:{login:"github-actions[bot]"}}' >> "$S/statuses/$sha.jsonl" ;;
  "api repos/"*"/pulls/"*) n="${2##*/}"; cat "$S/prs/$n.json" ;;
  "api repos/"*"/issues/"*"/events"*) n="$(cut -d/ -f5 <<<"$2")"; cat "$S/events/$n.jsonl" 2>/dev/null || true ;;
  "api repos/"*"/commits/"*"/statuses"*) sha="$(cut -d/ -f5 <<<"$2")"; jq -c '{description, created_at, creator}' "$S/statuses/$sha.jsonl" 2>/dev/null || true ;;
  "api repos/"*"/issues/"*"/comments"*) ;;
  *) ;;
esac
exit 0
GH
chmod +x "$T/bin/gh"
export PATH="$T/bin:$PATH" GHLOG="$T/gh.log" GHSTATE="$T/gh" GITHUB_REPOSITORY=sim/sim GITHUB_REPOSITORY_OWNER=AbnerCruz RUN_URL=https://example.invalid/run DEFAULT_BRANCH=main
O="$T/out.txt"
tick() { local n; n=$(( $(cat "$T/gh/clock") + 1 )); echo "$n" > "$T/gh/clock"; date -u -d "@$((1790000000 + n))" +%Y-%m-%dT%H:%M:%SZ; }
label_event() { # label_event <pr> <labeled|unlabeled> <ator> — evento de label feito por alguém de fora do integrador
  printf '{"event":"%s","label":{"name":"integrar"},"actor":{"login":"%s"},"created_at":"%s"}\n' "$2" "$3" "$(tick)" >> "$T/gh/events/$1.jsonl"
  if [ "$2" = labeled ]; then echo integrar >> "$T/gh/labels/$1"; else grep -vx integrar "$T/gh/labels/$1" > "$T/l" || true; mv "$T/l" "$T/gh/labels/$1"; fi
}
open_pr() { printf '{"state":"open","draft":false,"head":"%s"}\n' "$2" > "$T/gh/prs/$1.json"; touch "$T/gh/labels/$1"; git push -q -f origin "$2:refs/pull/$1/head"; }
last_status() { tail -n1 "$T/gh/statuses/$1.jsonl" 2>/dev/null | jq -r .description; }
plan() { # plan <pr> — a fila real (C#) decide sobre o estado simulado; exporta PLAN_* do resultado
  local main sha; main="$(git --git-dir="$T/origin.git" rev-parse main)"; sha="$(jq -r .head "$T/gh/prs/$1.json")"
  jq -n --arg m "$main" --arg n "$1" --arg h "$sha" --arg d "$(last_status "$sha")" --argjson l "$(jq -R . "$T/gh/labels/$1" | jq -s .)" \
    '{main:$m, prs:[{number:($n|tonumber), draft:false, crossRepository:false, headRef:"agente", headSha:$h, labels:$l, status:(if $d == "" or $d == "null" then null else {state:"x", description:$d} end)}]}' > "$T/in.json"
  dotnet run tests/consistency/Check.cs -- --integration-plan --input "$T/in.json" --policy docs/governance/integration-policy.json | grep -E '^[a-z_]+=' > "$T/plan.txt"
  PLAN_ACTION="$(sed -n 's/^action=//p' "$T/plan.txt")"; PLAN_COMBINED="$(sed -n 's/^tested_combined=//p' "$T/plan.txt")"
  PLAN_CRIT="$(sed -n 's/^tested_criticality=//p' "$T/plan.txt")"; PLAN_AUTH="$(sed -n 's/^authorized=//p' "$T/plan.txt")"
}
out() { sed -n "s/^$1=//p" "$O"; }
prep() {
  : > "$O"; git fetch -q origin main; git checkout -q --detach origin/main
  if env GITHUB_OUTPUT="$O" PR="$1" HEAD_SHA="$2" MAIN="$(git rev-parse origin/main)" \
      bash .github/integrator/integrate.sh prepare >"$T/prepare.log" 2>&1; then
    return 0
  else
    local code=$?
    echo "Falha ao preparar PR simulado $1 (exit $code):" >&2
    cat "$T/prepare.log" >&2
    return "$code"
  fi
}
finish_eval() { # finish_eval <pr> <head> [R_URBE] [R_HUB] — termina a avaliação com os checks do candidato verdes (ou o resultado dado)
  : > "$GHLOG"
  env GITHUB_OUTPUT=/dev/null ACTION=evaluate PR="$1" HEAD_SHA="$2" MAIN="$(out main_tested)" COMBINED="$(out combined)" CRITICALITY="$(out criticality)" \
    REQUIRES_OWNER="$(out requires_owner)" CRITICAL_CLASSES="$(out critical_classes)" CRITICAL_REASON="$(out critical_reason)" HANDOFFS="$(out handoffs)" \
    TRUSTED="$(out trusted)" TRUSTED_FAILURES="$(out trusted_failures)" AUTHORIZED="$(grep -qx integrar "$T/gh/labels/$1" && echo true || echo false)" MORE=false \
    R_CONSISTENCY=success R_URBE="${3:-skipped}" R_LUNET2D=skipped R_HUB="${4:-skipped}" bash .github/integrator/integrate.sh finish >"$T/finish.log" 2>&1
}
finish_land() { # finish_land <pr> — execução 'land' decidida pela fila real
  : > "$GHLOG"; plan "$1"
  [ "$PLAN_ACTION" = land ] || return 0
  env GITHUB_OUTPUT=/dev/null ACTION=land PR="$1" HEAD_SHA="$(jq -r .head "$T/gh/prs/$1.json")" MAIN="$(git --git-dir="$T/origin.git" rev-parse main)" \
    COMBINED="$PLAN_COMBINED" CRITICALITY="$PLAN_CRIT" REQUIRES_OWNER=true AUTHORIZED="$PLAN_AUTH" MORE=false bash .github/integrator/integrate.sh finish >/dev/null 2>&1
}
main_is() { [ "$(git --git-dir="$T/origin.git" rev-parse main)" = "$1" ]; }
in_main() { git --git-dir="$T/origin.git" merge-base --is-ancestor "$1" main; }
logged() { grep -q -- "$1" "$GHLOG"; }
handoff() { # handoff <id> [extra-json]: handoff válido em 'review' (portão NN-008 e checks confiáveis)
  python3 - "$1" "$(git rev-parse origin/main)" "${2:-}" <<'PY'
import json, sys, datetime
d = json.load(open('docs/governance/handoffs/HO-20261002-dec-0022-e-integracao.json'))
d.update(message_id=sys.argv[1], state='review', base_commit=sys.argv[2], commit=None, pr=None, task_id='P9-1',
         timestamp=(datetime.datetime.now(datetime.timezone.utc) - datetime.timedelta(hours=1)).strftime('%Y-%m-%dT%H:%M:%SZ'))
if sys.argv[3]: d.update(json.loads(sys.argv[3]))
json.dump(d, open(f'docs/governance/handoffs/{sys.argv[1]}.json', 'w'), ensure_ascii=False, indent=2)
PY
}
branch() { # branch <nome> <comando>: commit a partir da main atual com a mudança dada e um handoff; imprime o SHA
  git fetch -q origin main; git checkout -q -B "$1" origin/main; eval "$2"; handoff "HO-20991231-sim-$1"; G add -A; G commit -qm "$1"; git rev-parse HEAD
}

# 1. Rotina (Ecosystem): feature normal, checks verdes → integra sozinha, sem label, sem proprietário.
A="$(branch rotina 'echo "nota rotineira" >> README.md')"; open_pr 41 "$A"
prep 41 "$A"; CA="$(out combined)"
check "rotina: classificada pela política da main como 'routine', checker confiável verde" '[ "$(out criticality)" = routine ] && [ "$(out requires_owner)" = false ] && [ "$(out trusted)" = success ]'
check "estado combinado montado e publicado em integration/pr-41 (commit exato no status 'testando')" '[ "$(git --git-dir="$T/origin.git" rev-parse refs/heads/integration/pr-41)" = "$CA" ] && last_status "$A" | grep -q "^testando main=.* combined=$CA routine"'
finish_eval 41 "$A"
check "rotina verde: a main vira EXATAMENTE o commit testado, sem label nem proprietário" 'main_is "$CA" && in_main "$A" && last_status "$A" | grep -q "^integrado .*combined=$CA routine"'
check "depois de integrar: pages, consistency e a fila são redisparados" 'logged "workflow run pages.yml" && logged "workflow run consistency.yml" && logged "workflow run integrate.yml"'
check "rotina fora do Hub: não dispara release do Hub" '! logged "workflow run hub-release.yml"'

# 2. Rotina Urbe: bug normal em apps/urbe → CI do Urbe no estado combinado → integra sozinha.
U="$(branch urbe 'echo "// botão corrigido" >> apps/urbe/src/app.js')"; open_pr 42 "$U"
prep 42 "$U"; CU="$(out combined)"
check "rotina Urbe: CI do Urbe exigido e nenhuma autorização" '[ "$(out products)" = "[\"urbe\"]" ] && [ "$(out criticality)" = routine ]'
finish_eval 42 "$U" success
check "rotina Urbe verde: integrada automaticamente" 'main_is "$CU" && in_main "$U"'
check "Urbe integrado: não cria release artificial do Hub" '! logged "workflow run hub-release.yml"'

# 3. Crítico (control plane): PR que muda o integrador → verde, mas NÃO entra sozinho.
C="$(branch critico 'echo "# comentário" >> .github/workflows/integrate.yml')"; open_pr 43 "$C"
prep 43 "$C"; CC="$(out combined)"; M3="$(git --git-dir="$T/origin.git" rev-parse main)"
check "control plane: integrate.yml classificado como crítico pela política da main" '[ "$(out criticality)" = critical ] && grep -q control-plane <<<"$(out critical_classes)"'
finish_eval 43 "$C"
check "crítico verde: 'pronto', labels critico/pronto-para-integrar, main intocada" 'main_is "$M3" && last_status "$C" | grep -q "^pronto .*combined=$CC critical" && grep -qx critico "$T/gh/labels/43" && grep -qx pronto-para-integrar "$T/gh/labels/43"'
check "crítico verde: o portal é redisparado ('Precisa de você')" 'logged "workflow run pages.yml"'

# 4. Label de quem não é o proprietário → autorização inválida, label removida, nada entra.
label_event 43 labeled agente-com-token
finish_land 43
check "label 'integrar' posta por não-proprietário: inválida, removida, main intocada" 'main_is "$M3" && ! grep -qx integrar "$T/gh/labels/43" && logged "remove-label integrar"'

# 5. Proprietário autoriza → entra exatamente o commit combinado testado.
label_event 43 labeled AbnerCruz
finish_land 43
check "crítico autorizado pelo proprietário (evento conferido): main = commit combinado testado" 'main_is "$CC" && in_main "$C" && last_status "$C" | grep -q "^integrado .*combined=$CC critical"'

# 6. Ref de integração mutada depois do teste → não integra (não basta ter os mesmos pais).
X="$(branch mutacao 'mkdir -p docs/notes && echo "outra nota" > docs/notes/mutacao.md')"; open_pr 44 "$X"
prep 44 "$X"; CX="$(out combined)"; M6="$(git --git-dir="$T/origin.git" rev-parse main)"
B="$(G commit-tree "$CX^{tree}" -p "$CX^1" -p "$CX^2" -m "mesmos pais, outro commit")"; git push -q -f origin "$B:refs/heads/integration/pr-44"
finish_eval 44 "$X"
check "ref de integração ≠ commit testado (mesmos pais): NÃO integra, volta a testar" 'main_is "$M6" && ! in_main "$B" && last_status "$X" | grep -q "^testando"'

# 7. Corrida do head: A testado, autor envia B → A não entra; B é avaliado e entra.
HA="$(branch corrida 'mkdir -p docs/notes && echo "versão A" > docs/notes/corrida.md')"; open_pr 45 "$HA"
prep 45 "$HA"; M7="$(git --git-dir="$T/origin.git" rev-parse main)"
git checkout -q corrida; echo "versão B" >> docs/notes/corrida.md; G commit -qam "B"; HB="$(git rev-parse HEAD)"; open_pr 45 "$HB"
finish_eval 45 "$HA"
check "corrida do head: resultado de A não integra quando o PR já é B (A fica 'obsoleto')" 'main_is "$M7" && ! in_main "$HA" && last_status "$HA" | grep -q "^obsoleto"'
plan 45
check "corrida do head: a fila reavalia B" '[ "$PLAN_ACTION" = evaluate ]'
prep 45 "$HB"; CB="$(out combined)"; finish_eval 45 "$HB"
check "corrida do head: B testado entra (main = combinado de B)" 'main_is "$CB" && in_main "$HB"'

# 8. Corrida da main: M1 + PR testado, a main vira M2 → resultado de M1 não entra; reavaliado contra M2, entra.
R="$(branch mainrace 'echo "x" > MAINRACE.md')"; open_pr 46 "$R"
prep 46 "$R"; M1="$(git --git-dir="$T/origin.git" rev-parse main)"
git checkout -q --detach "$M1"; echo paralelo > PARALELO.txt; G add -A; G commit -qm paralelo; git push -q origin HEAD:refs/heads/main; M2="$(git rev-parse HEAD)"
finish_eval 46 "$R"
check "corrida da main: resultado de M1 não integra quando a main é M2" 'main_is "$M2" && ! in_main "$R" && last_status "$R" | grep -q "^testando"'
plan 46
check "corrida da main: a fila reavalia contra M2" '[ "$PLAN_ACTION" = evaluate ]'
prep 46 "$R"; CR="$(out combined)"; finish_eval 46 "$R"
check "corrida da main: reavaliado e integrado sobre M2 (com a mudança paralela)" 'main_is "$CR" && in_main "$M2" && in_main "$R"'

# 9. PR que enfraquece o checker: crítico, com o motivo apontado pela versão da main; não se autoaprova.
W="$(branch checker 'sed -i "/^        \"CHK-INTEGRATION\",$/d" tests/consistency/Check.cs')"; open_pr 47 "$W"
prep 47 "$W"; M9="$(git --git-dir="$T/origin.git" rev-parse main)"; finish_eval 47 "$W"
check "checker enfraquecido: crítico ('remove checks'), não entra sozinho" '[ "$(out criticality)" = critical ] && grep -q "remove checks: CHK-INTEGRATION" <<<"$(out critical_reason)" && main_is "$M9"'

# 10. PR que afrouxa a própria política: classificado pela política ANTERIOR (da main).
P="$(branch politica "jq '.classes = []' docs/governance/integration-policy.json > /tmp/p.json && cp /tmp/p.json docs/governance/integration-policy.json && echo x >> apps/urbe/src/persistence/backup.js")"; open_pr 48 "$P"
prep 48 "$P"; finish_eval 48 "$P"
check "política afrouxada: a política da main classifica o PR como crítico (control-plane e user-data)" '[ "$(out criticality)" = critical ] && grep -q control-plane <<<"$(out critical_classes)" && grep -q user-data <<<"$(out critical_classes)" && main_is "$M9"'

# 11. Conflito com a main atual → devolvido ao autor.
K="$(git checkout -q -B conflito "$MAIN" && echo "nota conflitante" >> README.md && handoff HO-20991231-sim-conflito && G add -A && G commit -qm conflito && git rev-parse HEAD)"; open_pr 49 "$K"
prep 49 "$K"
check "conflito com a main atual: bloqueado, arquivo apontado, nada publicado" '[ "$(out action)" = blocked ] && grep -q README.md <<<"$(out conflicts)" && main_is "$M9"'

# 12. Check do candidato vermelho → 'falhou', main intocada.
F="$(branch vermelho 'echo "// y" >> apps/urbe/src/app.js')"; open_pr 50 "$F"
prep 50 "$F"; finish_eval 50 "$F" failure
check "CI do Urbe vermelho no estado combinado: 'falhou', main intocada" 'main_is "$M9" && last_status "$F" | grep -q "^falhou"'

# 13. Hub (P3-3): mudança em apps/hub → Hub é o Product tocado → hub-ci exigido → verde integra; vermelho não mexe na main.
H="$(branch hub 'echo "// nota" >> apps/hub/src/Hub.Core/Datum.cs')"; open_pr 51 "$H"
prep 51 "$H"; CH="$(out combined)"
check "Hub: identificado como Product tocado (hub-ci exigido) e classificado pela política da main" '[ "$(out products)" = "[\"hub\"]" ] && [ "$(out criticality)" = routine ]'
HF="$(branch hub-vermelho 'echo "// quebra" >> apps/hub/src/Hub.Core/Datum.cs')"; open_pr 52 "$HF"
prep 52 "$HF"; M13="$(git --git-dir="$T/origin.git" rev-parse main)"; finish_eval 52 "$HF" skipped failure
check "hub-ci vermelho no estado combinado: 'falhou' citando hub-ci, main intocada" 'main_is "$M13" && last_status "$HF" | grep -q "^falhou" && last_status "$HF" | grep -q "hub-ci"'
check "hub-ci vermelho: nenhum dispatch de release" '! logged "workflow run hub-release.yml"'
prep 51 "$H"; CH="$(out combined)"; finish_eval 51 "$H" skipped success
check "hub-ci verde: integrada automaticamente (rotina), a main vira o commit testado" 'main_is "$CH" && in_main "$H"'
check "Hub integrado: dispara release existente na main" 'logged "workflow run hub-release.yml --repo sim/sim --ref main"'

# 14. Fechamento de handoff de trabalho crítico JÁ integrado (o caso do PR #46): a criticidade é da mudança atual, não do histórico.
#     O PR só muda estado/PR do handoff antigo (que continua declarando 'critical') → rotina → integra sozinho, sem label; as labels de
#     crítico de uma avaliação antiga saem.
git fetch -q origin main; git checkout -q --detach origin/main
handoff HO-20991231-sim-critico-integrado '{"criticality": {"declared": "critical", "classes": ["control-plane"], "reason": "mudou o integrador"}}'
G add -A; G commit -qm "trabalho crítico já integrado (com autorização)"; git push -q origin HEAD:refs/heads/main
git checkout -q -B fechamento origin/main
python3 - <<'PY'
import json
p = 'docs/governance/handoffs/HO-20991231-sim-critico-integrado.json'
d = json.load(open(p)); d.update(state='done', pr='https://github.com/AbnerCruz/Ecosystem/pull/43')
json.dump(d, open(p, 'w'), ensure_ascii=False, indent=2)
PY
G add -A; G commit -qm "fechamento"; K14="$(git rev-parse HEAD)"; open_pr 53 "$K14"; printf 'critico\npronto-para-integrar\n' > "$T/gh/labels/53"
prep 53 "$K14"; C14="$(out combined)"
check "fechamento de handoff crítico já integrado: rotina (o histórico 'critical' continua no handoff, mas não é herdado)" '[ "$(out criticality)" = routine ] && [ "$(out requires_owner)" = false ] && jq -e ".criticality.declared == \"critical\"" docs/governance/handoffs/HO-20991231-sim-critico-integrado.json >/dev/null'
finish_eval 53 "$K14"
check "fechamento integrado sozinho, sem label 'integrar'; labels antigas de crítico removidas" 'main_is "$C14" && in_main "$K14" && ! grep -qx critico "$T/gh/labels/53" && ! grep -qx pronto-para-integrar "$T/gh/labels/53" && ! grep -qx integrar "$T/gh/labels/53"'

# 15. Seleção: mesma autoridade de push.paths; todos os caminhos positivos e docs/testes/outra app negativos.
if python3 - <<'PYSEL'
import re
from pathlib import Path
script = Path('.github/integrator/integrate.sh').read_text().split('HUB_RELEASE_PATHS=(', 1)[1].split(')', 1)[0]
workflow = Path('.github/workflows/hub-release.yml').read_text().split('    paths:', 1)[1].split('  workflow_dispatch:', 1)[0]
a = re.findall(r"'([^']+)'", script)
b = re.findall(r"- '([^']+)'", workflow)
assert a and a == b, (a, b)
PYSEL
then check "filtro do integrador é projeção exata do push.paths de hub-release" true
else check "filtro do integrador divergiu do workflow de release" false; fi
sed '/^case "${1:-}" in/,$d' .github/integrator/integrate.sh > "$T/library.sh"
SELECT_MAIN="$(git --git-dir="$T/origin.git" rev-parse main)"
for path in apps/hub/src/Hub.Core/Datum.cs apps/hub/VERSION apps/hub/Directory.Build.props apps/hub/tools/dispatch-fixture.md .github/workflows/hub-release.yml apps/hub/README.md apps/hub/tests/dispatch-fixture.md apps/urbe/dispatch-fixture.md; do
  git checkout -q --detach "$SELECT_MAIN"
  printf '\n' >> "$path"; G add "$path"; G commit -qm "seletividade $path"
  : > "$GHLOG"
  env MAIN="$SELECT_MAIN" COMBINED="$(git rev-parse HEAD)" bash -c '. "$1"; dispatch_hub_release' bash "$T/library.sh" >"$T/selection.log" 2>&1
  case "$path" in
    apps/hub/README.md|apps/hub/tests/*|apps/urbe/*) check "$path: não cria release" '! logged "workflow run hub-release.yml"' ;;
    *) check "$path: dispatch de release" 'logged "workflow run hub-release.yml --repo sim/sim --ref main"' ;;
  esac
done

# P4-10: direct Lunet release selection and observable dispatch failure.
SELECT_MAIN="$(git rev-parse HEAD)"
for path in apps/lunet2d/README.md apps/lunet2d/src/Lunet.Core/dispatch-fixture.cs .github/workflows/lunet2d-release.yml apps/urbe/dispatch-fixture.md apps/hub/README.md; do
  git checkout -q -B "select-direct-${RANDOM}" "$SELECT_MAIN"
  mkdir -p "$(dirname "$path")"; echo "# fixture" >> "$path"; G add -A; G commit -qm "direct selection $path"
  : > "$GHLOG"
  env MAIN="$SELECT_MAIN" COMBINED="$(git rev-parse HEAD)" bash -c '. "$1"; dispatch_lunet_release' bash "$T/library.sh" >"$T/direct-selection.log" 2>&1
  case "$path" in
    apps/lunet2d/*|.github/workflows/lunet2d-release.yml) check "$path: direct Lunet release dispatch" 'logged "workflow run lunet2d-release.yml --repo sim/sim --ref main"' ;;
    *) check "$path: no direct Lunet release" '! logged "workflow run lunet2d-release.yml"' ;;
  esac
  check "$path: never releases Urbe on merge" '! logged "workflow run urbe-release.yml"'
done
git checkout -q -B select-direct-failure "$SELECT_MAIN"
echo "# fixture" >> apps/lunet2d/README.md; G add -A; G commit -qm 'dispatch failure'
if env FAIL_LUNET_DISPATCH=true MAIN="$SELECT_MAIN" COMBINED="$(git rev-parse HEAD)" bash -c '. "$1"; dispatch_lunet_release' bash "$T/library.sh" >/dev/null 2>&1; then
  check "direct Lunet dispatch failure must fail" false
else check "direct Lunet dispatch failure must fail" true; fi
git checkout -q main

# 16. Head do Hub muda depois do teste: não publica; depois retesta, integra e torna falha de dispatch observável.
HD="$(branch hub-dispatch 'echo "// dispatch" >> apps/hub/src/Hub.Core/Datum.cs')"; open_pr 54 "$HD"
prep 54 "$HD"; M16="$(git --git-dir="$T/origin.git" rev-parse main)"
git checkout -q hub-dispatch; echo "// head novo" >> apps/hub/src/Hub.Core/Datum.cs; G commit -qam "head novo"; HD2="$(git rev-parse HEAD)"; open_pr 54 "$HD2"
finish_eval 54 "$HD" skipped success
check "head do Hub mudou após teste: nada integra/publica" 'main_is "$M16" && ! logged "workflow run hub-release.yml" && last_status "$HD" | grep -q "^obsoleto"'
prep 54 "$HD2"; C16="$(out combined)"
export FAIL_HUB_DISPATCH=true
if finish_eval 54 "$HD2" skipped success; then check "dispatch recusado precisa reprovar o job" false
else check "dispatch recusado reprova o job" true; fi
unset FAIL_HUB_DISPATCH
check "dispatch recusado: código continua integrado, falha de publicação explícita, fila continua" 'main_is "$C16" && last_status "$HD2" | grep -q "^integrado" && grep -q "::error::Código integrado" "$T/finish.log" && logged "Publicação do APK pendente" && logged "workflow run integrate.yml"'

check "a main só avançou por fast-forward (nenhum force)" '( for x in "$MAIN" "$CA" "$CU" "$CC" "$CB" "$M2" "$CR" "$CH" "$C14" "$C16"; do in_main "$x" || exit 1; done )'

echo
if [ "$fails" -eq 0 ]; then echo "Simulação do integrador: todos os cenários passaram."; else echo "Simulação do integrador: $fails falha(s)."; exit 1; fi
