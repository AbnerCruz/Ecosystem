#!/usr/bin/env bash
# Integrador automático (ADR-0015): efeitos colaterais da fila de integração. As DECISÕES ficam em
# `tests/consistency/Check.cs -- --integration-plan | --integration-gates` (funções puras testadas pelo self-test).
# Sempre executado a partir da cópia CONFIÁVEL da main (diretório `trusted`); o código do PR nunca é executado aqui.
#
#   integrate.sh collect                 JSON com a main atual e os PRs abertos + status 'ecosystem/integration' (stdout)
#   integrate.sh prepare                 monta main+PR em ../work, aplica os portões, publica integration/pr-N (saída: $GITHUB_OUTPUT)
#   integrate.sh finish                  aplica o veredito: integra (push sem force), marca pronto, ou devolve ao autor; redispara a fila
#
# Variáveis: GH_TOKEN, GITHUB_REPOSITORY, GITHUB_OUTPUT, RUN_URL e, conforme o passo, PR, HEAD_SHA, MAIN, ACTION, COMBINED,
# POLICY, POLICY_REASON, GATE, GATE_REASON, AUTHORIZED, MORE, R_CONSISTENCY, R_URBE, R_LUNET2D.
set -euo pipefail

CONTEXT="ecosystem/integration"
MARKER="<!-- ecosystem-integration -->"
repo="${GITHUB_REPOSITORY}"
out="${GITHUB_OUTPUT:-/dev/null}"

git_bot() { git -c user.name="github-actions[bot]" -c user.email="41898282+github-actions[bot]@users.noreply.github.com" "$@"; }

status() { # status <sha> <state> <descrição>
  local desc="${3:0:139}"
  gh api -X POST "repos/${repo}/statuses/$1" -f state="$2" -f context="$CONTEXT" -f description="$desc" -f target_url="${RUN_URL:-}" >/dev/null
}

ensure_labels() {
  gh label create integrar --repo "$repo" --color 0E8A16 --description "Autorização do proprietário para o integrador levar o PR à main" --force >/dev/null
  gh label create pronto-para-integrar --repo "$repo" --color 1D76DB --description "Estado combinado com a main atual verde; aguardando autorização" --force >/dev/null
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
  local body; body="$(printf '%s\n\n%s\n\n---\n_Integrador automático do Ecosystem (ADR-0015) · [execução](%s)_\n' "$MARKER" "$(cat "$2")" "${RUN_URL:-}")"
  local id; id="$(gh api "repos/${repo}/issues/$1/comments" --paginate --jq ".[] | select(.body | contains(\"${MARKER}\")) | .id" | tail -n1)"
  if [ -n "$id" ]; then gh api -X PATCH "repos/${repo}/issues/comments/${id}" -f body="$body" >/dev/null
  else gh api -X POST "repos/${repo}/issues/$1/comments" -f body="$body" >/dev/null; fi
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
  local short="${MAIN:0:12}"
  git fetch -q origin "+refs/pull/${PR}/head:refs/remotes/pr/${PR}"
  if [ "$(git rev-parse "refs/remotes/pr/${PR}")" != "$HEAD_SHA" ]; then
    echo "action=none" >> "$out"; echo "O PR #${PR} mudou durante o planejamento; a próxima execução o reavalia."; return 0
  fi
  local title; title="$(gh pr view "$PR" --repo "$repo" --json title --jq .title)"
  git worktree remove --force ../work >/dev/null 2>&1 || true; rm -rf ../work; git worktree prune
  git worktree add -q --detach ../work "$MAIN"
  if ! git_bot -C ../work merge -q --no-ff -m "Integrar PR #${PR}: ${title}" "$HEAD_SHA" >/tmp/merge.log 2>&1; then
    git -C ../work diff --name-only --diff-filter=U > /tmp/conflicts.txt || true
    git -C ../work merge --abort || true
    status "$HEAD_SHA" failure "conflito main=${short}: $(paste -sd, /tmp/conflicts.txt)"
    { echo "action=blocked"; echo "outcome=conflito"; echo "conflicts=$(paste -sd, /tmp/conflicts.txt)"; } >> "$out"
    return 0
  fi
  git -C ../work diff --name-only "$MAIN" HEAD > /tmp/changed.txt
  dotnet run tests/consistency/Check.cs -- --integration-gates --base-root . --combined-root ../work --files /tmp/changed.txt | grep -E '^[a-z_]+=' > /tmp/gates.txt
  cat /tmp/gates.txt >> "$out"
  if ! grep -qx 'gate=ok' /tmp/gates.txt; then
    status "$HEAD_SHA" failure "bloqueado main=${short}: $(sed -n 's/^gate_reason=//p' /tmp/gates.txt)"
    { echo "action=blocked"; echo "outcome=bloqueado"; } >> "$out"
    return 0
  fi
  local combined; combined="$(git -C ../work rev-parse HEAD)"
  git -C ../work push -q --force origin "${combined}:refs/heads/integration/pr-${PR}"
  status "$HEAD_SHA" pending "testando main=${short}: estado combinado ${combined:0:7}"
  { echo "action=evaluate"; echo "combined=${combined}"; } >> "$out"
}

redispatch() { gh workflow run integrate.yml --repo "$repo" --ref "${DEFAULT_BRANCH:-main}" >/dev/null || true; }

finish() {
  local short="${MAIN:0:12}" body=/tmp/comment.md
  ensure_labels
  if [ "${ACTION}" = "blocked" ]; then
    if [ "${OUTCOME:-}" = "conflito" ]; then
      {
        echo "**Não integrado: conflito com a \`main\` atual** (\`${short}\`)."
        echo
        echo "Os mesmos trechos mudaram aqui e na \`main\`. A reconciliação é **semântica** e cabe ao agente autor: faça merge da \`main\` nesta branch, resolva pelo conteúdo (nunca \`--ours/--theirs\` às cegas em arquivo normativo), rode os checks e envie; o integrador reavalia sozinho a cada push."
        echo
        echo "Arquivos em conflito: \`${CONFLICTS:-?}\`"
        echo
        git fetch -q origin "+refs/pull/${PR}/head:refs/remotes/pr/${PR}" || true
        echo '```'
        dotnet run tests/consistency/Check.cs -- --integration "refs/remotes/pr/${PR}" --base "$MAIN" 2>/dev/null | grep -v warning || true
        echo '```'
      } > "$body"
    else
      echo "**Não integrado: ${GATE_REASON:-portão não atendido}** (estado combinado com \`main@${short}\`). Corrija e envie; o integrador reavalia a cada push." > "$body"
    fi
    labels "$PR" +precisa-reconciliar -pronto-para-integrar
    comment "$PR" "$body"
    [ "${MORE}" = "true" ] && redispatch
    return 0
  fi

  if [ "${ACTION}" = "evaluate" ]; then
    local failed=""
    [ "${R_CONSISTENCY}" = "success" ] || failed="${failed} consistency(${R_CONSISTENCY})"
    case "${R_URBE}" in success|skipped|"") ;; *) failed="${failed} urbe-checks(${R_URBE})" ;; esac
    case "${R_LUNET2D}" in success|skipped|"") ;; *) failed="${failed} lunet2d-ci(${R_LUNET2D})" ;; esac
    if [ -n "$failed" ]; then
      status "$HEAD_SHA" failure "falhou main=${short}:${failed}"
      echo "**Não integrado: checks vermelhos no estado combinado** (este PR + \`main@${short}\`):${failed}. Veja a [execução](${RUN_URL}); corrija e envie — o integrador reavalia a cada push." > "$body"
      labels "$PR" +precisa-reconciliar -pronto-para-integrar
      comment "$PR" "$body"
      [ "${MORE}" = "true" ] && redispatch
      return 0
    fi
  else
    # land: reaproveita o estado combinado já testado contra esta mesma main
    git fetch -q origin "refs/heads/integration/pr-${PR}"
    COMBINED="$(git rev-parse FETCH_HEAD)"
  fi

  if [ "${POLICY:-automatic}" != "automatic" ] && [ "${AUTHORIZED}" != "true" ]; then
    status "$HEAD_SHA" success "pronto main=${short}: verde; aguardando autorização (label integrar)"
    {
      echo "**Pronto para integrar.** O estado combinado (este PR + \`main@${short}\`) passou em todos os checks."
      echo
      echo "Falta a **autorização do proprietário**: ${POLICY_REASON:-política do componente}. Para autorizar, adicione a label \`integrar\` a este PR; o integrador leva exatamente o estado testado para a \`main\` (se a \`main\` mudar antes, ele testa de novo)."
    } > "$body"
    labels "$PR" +pronto-para-integrar -precisa-reconciliar
    comment "$PR" "$body"
    [ "${MORE}" = "true" ] && redispatch
    return 0
  fi

  # Integrar: a main só avança se ainda for exatamente a main testada (primeiro pai do commit combinado) — sem force.
  git fetch -q origin "${DEFAULT_BRANCH:-main}"
  local tip; tip="$(git rev-parse FETCH_HEAD)"
  if [ "$(git rev-parse "${COMBINED}^1")" != "$tip" ] || [ "$(git rev-parse "${COMBINED}^2")" != "$HEAD_SHA" ]; then
    status "$HEAD_SHA" pending "testando main=${tip:0:12}: a main mudou durante o teste; reavaliando"
    redispatch; return 0
  fi
  if ! git push -q origin "${COMBINED}:refs/heads/${DEFAULT_BRANCH:-main}"; then
    status "$HEAD_SHA" pending "testando main=${tip:0:12}: push recusado; reavaliando"
    redispatch; return 0
  fi
  status "$HEAD_SHA" success "integrado main=${short}: em ${COMBINED:0:7}"
  {
    echo "**Integrado** em \`${COMBINED:0:7}\` — exatamente o estado combinado testado (este PR + \`main@${short}\`)."
    echo
    echo "Se o PR mudou \`apps/<id>\`, a origem do Product sincroniza pelo agendamento dela; o portal mostra se o espelho estiver atrasado."
  } > "$body"
  labels "$PR" -pronto-para-integrar -precisa-reconciliar -integrar
  comment "$PR" "$body"
  git push -q origin --delete "integration/pr-${PR}" || true
  gh workflow run pages.yml --repo "$repo" --ref "${DEFAULT_BRANCH:-main}" >/dev/null || true
  gh workflow run consistency.yml --repo "$repo" --ref "${DEFAULT_BRANCH:-main}" >/dev/null || true
  redispatch  # a main mudou: os outros PRs precisam ser reavaliados contra ela
}

case "${1:-}" in
  collect) collect ;;
  prepare) prepare ;;
  finish) finish ;;
  *) echo "uso: integrate.sh collect|prepare|finish" >&2; exit 2 ;;
esac
