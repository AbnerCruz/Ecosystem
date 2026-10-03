#!/usr/bin/env bash
# Integrador automático (ADR-0015, ADD-0012): efeitos colaterais da fila de integração. As DECISÕES ficam em
# `tests/consistency/Check.cs -- --integration-plan | --integration-gates | --integration-authorization` (funções puras testadas pelo
# self-test) e a POLÍTICA em docs/governance/integration-policy.json — sempre as da main (diretório `trusted`); o código do PR nunca
# é executado aqui. Rotina verde entra sozinha; crítico verde espera a autorização do proprietário.
#
#   integrate.sh collect                 JSON com a main atual e os PRs abertos + status 'ecosystem/integration' (stdout)
#   integrate.sh prepare                 monta main+PR em ../work, classifica, roda os checks confiáveis, publica integration/pr-N
#   integrate.sh finish                  aplica o veredito: integra (push sem force do commit EXATO testado), pede autorização ou devolve ao autor
#
# Status no head do PR: "<resultado> main=<sha> combined=<sha|-> <routine|critical|->[: detalhe]" — combined é o commit exato testado.
# Variáveis: GH_TOKEN, GITHUB_REPOSITORY, GITHUB_REPOSITORY_OWNER, GITHUB_OUTPUT, RUN_URL e, conforme o passo, PR, HEAD_SHA, MAIN, ACTION,
# COMBINED, CRITICALITY, REQUIRES_OWNER, CRITICAL_CLASSES, CRITICAL_REASON, GATE_REASON, HANDOFFS, TRUSTED, TRUSTED_FAILURES, MORE,
# R_CONSISTENCY, R_URBE, R_LUNET2D, R_HUB, R_ECOSYSTEM_AI.
set -euo pipefail

CONTEXT="ecosystem/integration"
MARKER="<!-- ecosystem-integration -->"
POLICY="docs/governance/integration-policy.json"
repo="${GITHUB_REPOSITORY}"
owner="${GITHUB_REPOSITORY_OWNER:-${repo%%/*}}"
out="${GITHUB_OUTPUT:-/dev/null}"
trusted="$(pwd)"
LABEL="$(jq -r '.authorization.label // "integrar"' "$POLICY" 2>/dev/null || echo integrar)"

git_bot() { git -c user.name="github-actions[bot]" -c user.email="41898282+github-actions[bot]@users.noreply.github.com" "$@"; }
check() { dotnet run "$trusted/tests/consistency/Check.cs" -- "$@"; }

status() { # status <sha> <state> <resultado> <main> <combined|-> <routine|critical|-> [detalhe]
  local desc="$3 main=$4 combined=$5 $6"
  [ -n "${7:-}" ] && desc="${desc}: $7"
  gh api -X POST "repos/${repo}/statuses/$1" -f state="$2" -f context="$CONTEXT" -f description="${desc:0:139}" -f target_url="${RUN_URL:-}" >/dev/null
}

ensure_labels() {
  gh label create "$LABEL" --repo "$repo" --color 0E8A16 --description "Autorização do proprietário para uma mudança CRÍTICA (só vale se posta por ele)" --force >/dev/null
  gh label create critico --repo "$repo" --color B60205 --description "Classificada como crítica pela política da main (ADD-0012)" --force >/dev/null
  gh label create pronto-para-integrar --repo "$repo" --color 1D76DB --description "Crítica, verde no estado combinado; aguardando autorização do proprietário" --force >/dev/null
  gh label create precisa-reconciliar --repo "$repo" --color D93F0B --description "Conflito ou checks vermelhos no estado combinado; ação do agente autor" --force >/dev/null
}

labels() { # labels <pr> <+add|-remove>...
  local pr="$1"; shift
  for l in "$@"; do
    case "$l" in
      +*) gh pr edit "$pr" --repo "$repo" --add-label "${l#+}" >/dev/null 2>&1 || true ;;
      -*) gh pr edit "$pr" --repo "$repo" --remove-label "${l#-}" >/dev/null 2>&1 || true ;;
    esac
  done
}

comment() { # comment <pr> <arquivo-com-corpo>: um único comentário do integrador por PR, atualizado a cada avaliação
  local body; body="$(printf '%s\n\n%s\n\n---\n_Integrador automático do Ecosystem (ADR-0015, ADD-0012) · [execução](%s)_\n' "$MARKER" "$(cat "$2")" "${RUN_URL:-}")"
  local id; id="$(gh api "repos/${repo}/issues/$1/comments" --paginate --jq ".[] | select(.body | contains(\"${MARKER}\")) | .id" | tail -n1)"
  if [ -n "$id" ]; then gh api -X PATCH "repos/${repo}/issues/comments/${id}" -f body="$body" >/dev/null
  else gh api -X POST "repos/${repo}/issues/$1/comments" -f body="$body" >/dev/null; fi
}

identity() { # bloco de rastreabilidade (visível ≠ precisa de autorização)
  echo "| | |"
  echo "|---|---|"
  echo "| main testada | \`${MAIN}\` |"
  echo "| head do PR testado | \`${HEAD_SHA}\` |"
  echo "| estado combinado testado | \`${COMBINED:--}\` |"
  echo "| classificação | ${CRITICALITY:-?}${CRITICAL_CLASSES:+ ${CRITICAL_CLASSES}} |"
  echo "| handoff | ${HANDOFFS:-[]} |"
  echo "| checks | [execução](${RUN_URL:-}) · consistency=${R_CONSISTENCY:-?} · urbe=${R_URBE:-?} · lunet2d=${R_LUNET2D:-?} · hub=${R_HUB:-?} · ecosystem-ai=${R_ECOSYSTEM_AI:-?} · confiável(main)=${TRUSTED:-?} |"
}

collect() {
  local main; main="$(git rev-parse HEAD)"
  local prs; prs="$(gh pr list --repo "$repo" --state open --limit 100 --json number,isDraft,isCrossRepository,headRefName,headRefOid,labels)"
  local items="[]"
  while read -r pr; do
    [ -z "$pr" ] && continue
    local sha st
    sha="$(jq -r '.headRefOid' <<<"$pr")"
    st="$(gh api "repos/${repo}/commits/${sha}/status" --jq "[.statuses[] | select(.context==\"${CONTEXT}\")][0] // null")"
    items="$(jq -c --argjson pr "$pr" --argjson st "${st:-null}" '. + [{
      number: $pr.number, draft: $pr.isDraft, crossRepository: $pr.isCrossRepository, headRef: $pr.headRefName,
      headSha: $pr.headRefOid, labels: [$pr.labels[].name],
      status: (if $st == null then null else {state: $st.state, description: $st.description} end) }]' <<<"$items")"
  done < <(jq -c '.[]' <<<"$prs")
  jq -n --arg main "$main" --argjson prs "$items" '{main: $main, prs: $prs}'
}

prepare() {
  git fetch -q origin "+refs/pull/${PR}/head:refs/remotes/pr/${PR}"
  if [ "$(git rev-parse "refs/remotes/pr/${PR}")" != "$HEAD_SHA" ]; then
    echo "action=none" >> "$out"; echo "O PR #${PR} mudou durante o planejamento; a próxima execução o reavalia."; return 0
  fi
  local title; title="$(gh pr view "$PR" --repo "$repo" --json title --jq .title)"
  git worktree remove --force ../work >/dev/null 2>&1 || true; rm -rf ../work; git worktree prune
  git worktree add -q --detach ../work "$MAIN"
  if ! git_bot -C ../work merge -q --no-ff --no-commit "$HEAD_SHA" >/tmp/merge.log 2>&1; then
    git -C ../work diff --name-only --diff-filter=U > /tmp/conflicts.txt || true
    git -C ../work merge --abort || true
    status "$HEAD_SHA" failure conflito "$MAIN" - - "$(paste -sd, /tmp/conflicts.txt)"
    { echo "action=blocked"; echo "outcome=conflito"; echo "conflicts=$(paste -sd, /tmp/conflicts.txt)"; } >> "$out"
    return 0
  fi
  git -C ../work diff --cached --name-only "$MAIN" > /tmp/changed.txt
  # Portões e criticidade: política e regras da MAIN (--base-root . = cópia confiável), aplicadas à árvore candidata.
  check --integration-gates --base-root . --combined-root ../work --files /tmp/changed.txt | grep -E '^[a-z_]+=' > /tmp/gates.txt
  cat /tmp/gates.txt >> "$out"
  local criticality classes handoffs
  criticality="$(sed -n 's/^criticality=//p' /tmp/gates.txt)"; classes="$(sed -n 's/^critical_classes=//p' /tmp/gates.txt)"
  handoffs="$(sed -n 's/^handoffs=//p' /tmp/gates.txt)"
  if ! grep -qx 'gate=ok' /tmp/gates.txt; then
    git -C ../work merge --abort || true
    status "$HEAD_SHA" failure bloqueado "$MAIN" - "$criticality" "$(sed -n 's/^gate_reason=//p' /tmp/gates.txt)"
    { echo "action=blocked"; echo "outcome=bloqueado"; } >> "$out"
    return 0
  fi
  # O commit combinado registra no próprio histórico de onde veio e como foi classificado (auditável sem o GitHub).
  git_bot -C ../work commit -q -m "Integrar PR #${PR}: ${title}" \
    -m "Integration-Criticality: ${criticality} ${classes}" -m "Integration-Main: ${MAIN}" -m "Integration-PR-Head: ${HEAD_SHA}" -m "Integration-Handoffs: ${handoffs}"
  local combined; combined="$(git -C ../work rev-parse HEAD)"
  # Checker confiável: os checks da MAIN sobre a árvore candidata (além dos checks do próprio candidato, que rodam depois).
  local trusted_result=success failures=""
  if ! (cd ../work && dotnet run "$trusted/tests/consistency/Check.cs") > /tmp/trusted.txt 2>&1; then
    trusted_result=failure; failures="$(grep -E '^FAIL ' /tmp/trusted.txt | awk '{print $2}' | paste -sd, || true)"
  fi
  git -C ../work push -q --force origin "${combined}:refs/heads/integration/pr-${PR}"
  status "$HEAD_SHA" pending testando "$MAIN" "$combined" "$criticality"
  { echo "action=evaluate"; echo "main_tested=${MAIN}"; echo "combined=${combined}"; echo "trusted=${trusted_result}"; echo "trusted_failures=${failures}"; } >> "$out"
}

# Projeção do filtro push de hub-release.yml; a simulação confere igualdade com a autoridade.
HUB_RELEASE_PATHS=(
  'apps/hub/src/**'
  'apps/hub/VERSION'
  'apps/hub/Directory.Build.props'
  'apps/hub/tools/**'
  '.github/workflows/hub-release.yml'
)

dispatch_hub_release() {
  local paths=() path result=0
  for path in "${HUB_RELEASE_PATHS[@]}"; do paths+=(":(glob)$path"); done
  # O diff é do estado EXATO testado, inclusive na execução land com checkout separado do prepare.
  git diff --quiet "$MAIN" "$COMBINED" -- "${paths[@]}" || result=$?
  case "$result" in
    0) return 0 ;;  # docs/testes isolados e outros Products não criam release do Hub (NN-014)
    1) gh workflow run hub-release.yml --repo "$repo" --ref "${DEFAULT_BRANCH:-main}" ;;
    *) echo "Não foi possível verificar os arquivos do Hub no estado integrado." >&2; return 1 ;;
  esac
}

# P4-10: GITHUB_TOKEN pushes do not trigger Product publication. Only Lunet auto-releases;
# Urbe release remains an explicitly approved tag, independent of integration (REQ-006/066).
dispatch_lunet_release() {
  local changed
  changed="$(git diff --name-only "$MAIN" "$COMBINED" -- apps/lunet2d .github/workflows/lunet2d-release.yml)" || return 1
  [ -n "$changed" ] || return 0
  gh workflow run lunet2d-release.yml --repo "$repo" --ref "${DEFAULT_BRANCH:-main}"
}

redispatch() { gh workflow run integrate.yml --repo "$repo" --ref "${DEFAULT_BRANCH:-main}" >/dev/null || true; }

# --- guardas: a main só avança para o commit EXATO testado, do head ainda atual do PR, sobre a main ainda atual -----------------
guard_pr_head() { # o PR real ainda aponta para o head testado (corrida: autor enviou outro commit)
  local pr; pr="$(gh api "repos/${repo}/pulls/${PR}" --jq '{state: .state, draft: .draft, head: .head.sha}')"
  [ "$(jq -r .state <<<"$pr")" = open ] && [ "$(jq -r .draft <<<"$pr")" = false ] && [ "$(jq -r .head <<<"$pr")" = "$HEAD_SHA" ]
}
guard_combined() { # a ref de integração ainda é exatamente o commit testado (não basta ter os mesmos pais)
  git fetch -q origin "+refs/heads/integration/pr-${PR}:refs/remotes/integration/pr-${PR}" || return 1
  [ -n "$COMBINED" ] && [ "$(git rev-parse "refs/remotes/integration/pr-${PR}")" = "$COMBINED" ]
}
guard_main() { # a main atual é a main testada (primeiro pai) e o segundo pai é o head testado
  git fetch -q origin "${DEFAULT_BRANCH:-main}" || return 1
  [ "$(git rev-parse FETCH_HEAD)" = "$MAIN" ] && [ "$(git rev-parse "${COMBINED}^1")" = "$MAIN" ] && [ "$(git rev-parse "${COMBINED}^2")" = "$HEAD_SHA" ]
}
guard_authorization() { # crítico: a label vale só se o EVENTO foi de um autorizador, depois de este head ser avaliado
  gh api "repos/${repo}/issues/${PR}/events" --paginate --jq '.[] | select(.event=="labeled" or .event=="unlabeled") | {event, label: {name: .label.name}, actor: {login: .actor.login}, created_at}' | jq -s . > /tmp/events.json
  gh api "repos/${repo}/commits/${HEAD_SHA}/statuses" --paginate --jq ".[] | select(.context==\"${CONTEXT}\") | {description, created_at, creator: {login: .creator.login}}" | jq -s . > /tmp/statuses.json
  check --integration-authorization --events /tmp/events.json --statuses /tmp/statuses.json --owner "$owner" --policy "$POLICY" | grep -E '^[a-z_]+=' > /tmp/auth.txt
  grep -qx 'authorized=true' /tmp/auth.txt
}

land() {
  local body=/tmp/comment.md
  if ! guard_pr_head; then
    status "$HEAD_SHA" success obsoleto "$MAIN" "$COMBINED" "$CRITICALITY" "o PR mudou depois do teste; o head novo é avaliado"
    redispatch; return 0
  fi
  if ! guard_combined; then
    status "$HEAD_SHA" pending testando "$MAIN" "$COMBINED" "$CRITICALITY" "ref de integração diferente do commit testado; retestando"
    redispatch; return 0
  fi
  if ! guard_main; then
    status "$HEAD_SHA" pending testando "$MAIN" "$COMBINED" "$CRITICALITY" "a main mudou durante o teste; reavaliando"
    redispatch; return 0
  fi
  if [ "${REQUIRES_OWNER}" = "true" ] && ! guard_authorization; then
    local why; why="$(sed -n 's/^authorization_reason=//p' /tmp/auth.txt)"
    labels "$PR" "-$LABEL"   # label inválida não fica: evita laço e deixa claro que precisa ser posta de novo pelo proprietário
    status "$HEAD_SHA" success pronto "$MAIN" "$COMBINED" "$CRITICALITY" "aguardando autorização válida"
    { echo "**Autorização inválida — não integrado.** ${why}"; echo; identity; } > "$body"
    comment "$PR" "$body"; return 0
  fi
  if ! git push -q origin "${COMBINED}:refs/heads/${DEFAULT_BRANCH:-main}"; then
    status "$HEAD_SHA" pending testando "$MAIN" "$COMBINED" "$CRITICALITY" "push recusado; reavaliando"
    redispatch; return 0
  fi
  local by=""; [ "${REQUIRES_OWNER}" = "true" ] && by=" — $(sed -n 's/^authorization_reason=//p' /tmp/auth.txt)"
  status "$HEAD_SHA" success integrado "$MAIN" "$COMBINED" "$CRITICALITY"
  {
    if [ "${REQUIRES_OWNER}" = "true" ]; then echo "**Integrado** (crítico${by})."
    else echo "**Integrado automaticamente** (rotina): nenhum toque humano necessário (ADD-0012)."; fi
    echo
    echo "A \`main\` agora é exatamente o estado combinado testado."
    echo
    identity
  } > "$body"
  labels "$PR" "-pronto-para-integrar" "-precisa-reconciliar" "-$LABEL"
  comment "$PR" "$body"
  git push -q origin --delete "integration/pr-${PR}" || true
  gh workflow run pages.yml --repo "$repo" --ref "${DEFAULT_BRANCH:-main}" >/dev/null || true
  gh workflow run consistency.yml --repo "$repo" --ref "${DEFAULT_BRANCH:-main}" >/dev/null || true
  local publication_result=0
  if ! dispatch_hub_release; then
    publication_result=1
    echo "::error::Código integrado, mas dispatch de hub-release falhou; publicação do APK pendente."
    echo >> "$body"
    echo "**Publicação do APK pendente:** falhou o dispatch de hub-release. O código foi integrado; execute o workflow na main e confira a release antes de declarar publicação." >> "$body"
    comment "$PR" "$body"
  fi
  if ! dispatch_lunet_release; then
    publication_result=1
    echo "::error::Código integrado, mas dispatch de lunet2d-release falhou."
    echo "**Publicação do Lunet pendente:** execute lunet2d-release na main e confira a release." >> "$body"
    comment "$PR" "$body"
  fi
  redispatch  # a main mudou: os outros PRs precisam ser reavaliados contra ela, mesmo se publicação falhar
  return "$publication_result"
}

critical_explanation() { # o que o proprietário precisa decidir — em termos da decisão, não do código
  echo "**Mudança crítica pronta e testada — precisa da sua autorização.**"
  echo
  echo "Todos os checks passaram no estado combinado (este PR + \`main\` atual). Pela política da \`main\` ([ADD-0012](https://github.com/${repo}/blob/${DEFAULT_BRANCH:-main}/docs/governance/addenda/ADD-0012-automatico-por-padrao-humano-para-o-critico.md)), ela não entra sozinha porque:"
  echo
  jq -r --argjson cls "${CRITICAL_CLASSES:-[]}" '.classes[] | select(.id as $i | $cls | index($i)) | "- **\(.title)** — \(.consequence)"' "$POLICY" 2>/dev/null || true
  echo
  echo "Detalhes: ${CRITICAL_REASON:-—}"
  if [ "${TRUSTED:-success}" != success ]; then
    echo
    echo "⚠️ A versão confiável da \`main\` reprova o estado combinado em: \`${TRUSTED_FAILURES:-?}\`. Esperado só se este PR muda exatamente essas regras; senão, não autorize."
  fi
  echo
  echo "**Se autorizar** (label \`${LABEL}\`, posta por você): entra exatamente o commit testado abaixo; se a \`main\` ou o PR mudarem antes, o integrador testa de novo e pede de novo."
  echo "**Se não autorizar:** nada entra; o PR fica aberto. Se for preciso escolher uma direção nova, o agente abre uma decisão no portal."
  echo
  identity
}

finish() {
  local body=/tmp/comment.md
  ensure_labels
  if [ "${ACTION}" = "blocked" ]; then
    if [ "${OUTCOME:-}" = "conflito" ]; then
      {
        echo "**Não integrado: conflito com a \`main\` atual** (\`${MAIN:0:12}\`)."
        echo
        echo "Os mesmos trechos mudaram aqui e na \`main\`. A reconciliação é **semântica** e cabe ao agente autor: faça merge da \`main\` nesta branch, resolva pelo conteúdo (nunca \`--ours/--theirs\` às cegas em arquivo normativo), rode os checks e envie; o integrador reavalia sozinho a cada push."
        echo
        echo "Arquivos em conflito: \`${CONFLICTS:-?}\`"
      } > "$body"
    else
      echo "**Não integrado: ${GATE_REASON:-portão não atendido}** (estado combinado com \`main@${MAIN:0:12}\`). Corrija e envie; o integrador reavalia a cada push." > "$body"
    fi
    labels "$PR" +precisa-reconciliar -pronto-para-integrar
    comment "$PR" "$body"
    [ "${MORE}" = "true" ] && redispatch
    return 0
  fi

  if [ "${ACTION}" = "evaluate" ]; then
    # Reclassificado como rotina (ex.: avaliação anterior crítica): labels de crítico não ficam penduradas no PR.
    [ "${REQUIRES_OWNER}" = "true" ] || labels "$PR" -critico -pronto-para-integrar
    local failed=""
    [ "${R_CONSISTENCY}" = "success" ] || failed="${failed} consistency(${R_CONSISTENCY})"
    case "${R_URBE}" in success|skipped|"") ;; *) failed="${failed} urbe-checks(${R_URBE})" ;; esac
    case "${R_LUNET2D}" in success|skipped|"") ;; *) failed="${failed} lunet2d-ci(${R_LUNET2D})" ;; esac
    case "${R_HUB:-}" in success|skipped|"") ;; *) failed="${failed} hub-ci(${R_HUB})" ;; esac
    case "${R_ECOSYSTEM_AI:-}" in success|skipped|"") ;; *) failed="${failed} ecosystem-ai-ci(${R_ECOSYSTEM_AI})" ;; esac
    # Rotina: os checks da main sobre o candidato também precisam passar. Crítico: a divergência vira informação para o proprietário.
    [ "${TRUSTED:-success}" = success ] || [ "${REQUIRES_OWNER}" = "true" ] || failed="${failed} confiável-main(${TRUSTED_FAILURES:-?})"
    if [ -n "$failed" ]; then
      status "$HEAD_SHA" failure falhou "$MAIN" "$COMBINED" "$CRITICALITY" "${failed# }"
      { echo "**Não integrado: checks vermelhos no estado combinado** (este PR + \`main@${MAIN:0:12}\`):${failed}. Corrija e envie — o integrador reavalia a cada push."; echo; identity; } > "$body"
      labels "$PR" +precisa-reconciliar -pronto-para-integrar
      comment "$PR" "$body"
      [ "${MORE}" = "true" ] && redispatch
      return 0
    fi
    if [ "${REQUIRES_OWNER}" != "true" ]; then land; return 0; fi
    # Crítico verde: se o proprietário já autorizou ESTE head (evento posterior à avaliação), integra; senão pede.
    local stale=""
    if [ "${AUTHORIZED}" = "true" ]; then
      if guard_authorization; then land; return 0; fi
      labels "$PR" "-$LABEL"
      stale="A label \`${LABEL}\` que estava no PR não vale para este estado: $(sed -n 's/^authorization_reason=//p' /tmp/auth.txt)."
    fi
    status "$HEAD_SHA" success pronto "$MAIN" "$COMBINED" "$CRITICALITY" "aguardando autorização do proprietário"
    { critical_explanation; [ -n "$stale" ] && { echo; echo "$stale"; }; true; } > "$body"
    labels "$PR" +critico +pronto-para-integrar -precisa-reconciliar
    comment "$PR" "$body"
    gh workflow run pages.yml --repo "$repo" --ref "${DEFAULT_BRANCH:-main}" >/dev/null || true  # "Precisa de você" no portal
    [ "${MORE}" = "true" ] && redispatch
    return 0
  fi

  # land: crítico já testado contra esta main e com a label; COMBINED vem do status do PR (o commit exato testado).
  land
}

case "${1:-}" in
  collect) collect ;;
  prepare) prepare ;;
  finish) finish ;;
  *) echo "uso: integrate.sh collect|prepare|finish" >&2; exit 2 ;;
esac
