#!/usr/bin/env bash
# Sincroniza o repositório de ORIGEM de um produto com apps/<id> do Ecosystem (plano de importação §7, DEC-0014-A).
#
# Esta é a cópia canônica (NN-001). A cópia que roda na origem declara "Copiado de Ecosystem@<commit>" e nunca é editada lá.
# Roda DENTRO da origem (checkout da branch padrão), só com o GITHUB_TOKEN da própria origem; lê o Ecosystem (público) sem token.
#
# Variáveis:  COMPONENT (urbe|lunet2d, obrigatória)   ECOSYSTEM_REPO (padrão: https://github.com/AbnerCruz/Ecosystem.git)
#             BRANCH (padrão: main)   RELEASE_MODE (ci-dispatch | tag-dispatch | none; padrão: none)   GH_TOKEN, GITHUB_REPOSITORY
#             DRY_RUN=1 não empurra nem dispara nada (ensaio local)
# O que faz, nesta ordem:
#   1. Deriva: se a origem recebeu mudanças humanas fora de .github desde a última sincronização, ABRE uma Issue e para.
#   2. Espelha o conteúdo de apps/<id> (exceto .github) em um commit do bot, com os trailers Ecosystem-Commit e Ecosystem-Tree.
#   3. tag-dispatch: para cada tag <id>/vX do Ecosystem ainda sem tag vX na origem, espelha a árvore daquela tag, cria vX
#      no commit espelhado e dispara release.yml sobre ela. ci-dispatch: dispara ci.yml na branch (pushes do GITHUB_TOKEN não
#      disparam workflows; workflow_dispatch é a exceção documentada).
set -euo pipefail

: "${COMPONENT:?COMPONENT é obrigatória}"
case "$COMPONENT" in *[!a-z0-9]*|"") echo "COMPONENT inválido"; exit 2;; esac
ECOSYSTEM_REPO="${ECOSYSTEM_REPO:-https://github.com/AbnerCruz/Ecosystem.git}"
BRANCH="${BRANCH:-main}"
RELEASE_MODE="${RELEASE_MODE:-none}"
DRY_RUN="${DRY_RUN:-0}"
BOT_NAME="github-actions[bot]"; BOT_EMAIL="41898282+github-actions[bot]@users.noreply.github.com"

say() { echo "[sync] $*"; }
run() { if [ "$DRY_RUN" = "1" ]; then say "(ensaio) $*"; else "$@"; fi; }

git config user.name "$BOT_NAME"; git config user.email "$BOT_EMAIL"
git checkout -q "$BRANCH"

say "buscando $ECOSYSTEM_REPO"
git fetch -q --no-tags "$ECOSYSTEM_REPO" "+refs/heads/main:refs/ecosystem/main" "+refs/tags/${COMPONENT}/*:refs/ecosystem/tags/*"
target="$(git rev-parse refs/ecosystem/main)"

trailer() { git log -n1 --format="%(trailers:key=$2,valueonly)" "$1" | tr -d '[:space:]'; }
last="$(git log -n1 --format=%H --grep='^Ecosystem-Tree: ' "$BRANCH" || true)"

# 1. Deriva -----------------------------------------------------------------------------------------------------------
if [ -n "$last" ]; then
  drift="$(git diff --name-only "$last" HEAD -- . ':(exclude).github' || true)"
  if [ -n "$drift" ]; then
    say "DERIVA: a origem mudou fora de .github depois da última sincronização:"; echo "$drift"
    if [ "$DRY_RUN" != "1" ]; then
      if ! gh issue list --state open --search "Deriva da origem em:title" --json number --jq '.[0].number' | grep -q .; then
        gh issue create --title "Deriva da origem: mudança fora do Ecosystem" \
          --body "O código deste repositório é um espelho de \`apps/${COMPONENT}\` do Ecosystem (DEC-0014-A). Há mudanças feitas aqui depois da última sincronização (${last:0:7}), fora de \`.github/\`:

\`\`\`
${drift}
\`\`\`

A sincronização foi **interrompida** para não sobrescrevê-las. Leve a mudança para o Ecosystem (PR em \`apps/${COMPONENT}\`) e reverta-a aqui." || true
      fi
    fi
    exit 1
  fi
fi

# --- espelho --------------------------------------------------------------------------------------------------------------
tree_of() { git rev-parse "$1:apps/${COMPONENT}"; }
mirror() { # $1 = commit do Ecosystem
  git rm -rq --quiet -- . ':(exclude).github' 2>/dev/null || true
  git archive --format=tar "$1:apps/${COMPONENT}" | tar -x --anchored --exclude='.github'
  git add -A
}
commit_mirror() { # $1 = commit do Ecosystem, $2 = rótulo; retorna 0 se criou commit
  local sha="$1" tree; tree="$(tree_of "$sha")"
  mirror "$sha"
  if git diff --cached --quiet && [ -n "$(git log -n1 --format=%H --grep="^Ecosystem-Tree: ${tree}\$" "$BRANCH" || true)" ]; then return 1; fi
  git commit -q --allow-empty -m "Sincronizar com Ecosystem@${sha:0:7}${2:+ ($2)}" \
    -m "Espelho de apps/${COMPONENT} (DEC-0014-A). Não edite aqui: a autoridade do código é o Ecosystem." \
    -m "Ecosystem-Commit: ${sha}
Ecosystem-Tree: ${tree}"
  return 0
}

# Primeira execução: o espelho precisa ser idêntico à origem atual (fora de .github), senão NÃO empurra nada.
if [ -z "$last" ]; then
  mirror "$target"
  if ! git diff --cached --quiet; then
    say "PRIMEIRA SINCRONIZAÇÃO RECUSADA: o espelho difere da origem atual (a importação não coincide):"
    git diff --cached --stat | tail -n 15
    git reset -q --hard HEAD; exit 1
  fi
  say "primeira sincronização: espelho idêntico à origem; registrando âncora (sem disparar release)"
  bootstrap=1
fi

pushed=0
bootstrap="${bootstrap:-0}"
# 3a. Tags de release pendentes (tag-dispatch)
if [ "$RELEASE_MODE" = "tag-dispatch" ]; then
  for ref in $(git for-each-ref --format='%(refname)' refs/ecosystem/tags | sed 's#refs/ecosystem/tags/##' | sort -V); do
    git rev-parse -q --verify "refs/tags/${ref}" >/dev/null && continue
    tag_commit="$(git rev-parse "refs/ecosystem/tags/${ref}^{commit}")"
    say "release pendente: ${ref} (Ecosystem@${tag_commit:0:7})"
    commit_mirror "$tag_commit" "release ${ref}" || git commit -q --allow-empty -m "Sincronizar com Ecosystem@${tag_commit:0:7} (release ${ref})" -m "Ecosystem-Commit: ${tag_commit}
Ecosystem-Tree: $(tree_of "$tag_commit")"
    git tag "$ref"
    run git push -q origin "HEAD:${BRANCH}" "refs/tags/${ref}"
    run gh workflow run release.yml --ref "$ref"
    pushed=1
  done
fi

# 2. Sincronização da branch -------------------------------------------------------------------------------------------
if commit_mirror "$target" ""; then
  run git push -q origin "HEAD:${BRANCH}"; pushed=1
  say "branch sincronizada com Ecosystem@${target:0:7}"
  case "$RELEASE_MODE" in
    ci-dispatch) [ "$bootstrap" = "1" ] || run gh workflow run ci.yml --ref "$BRANCH";;
  esac
else
  say "nada a sincronizar (árvore de apps/${COMPONENT} igual à última sincronizada)"
fi

# Pages por branch: push do GITHUB_TOKEN pode não disparar a construção; pede explicitamente (melhor esforço).
if [ "$pushed" = "1" ] && [ "${REQUEST_PAGES_BUILD:-0}" = "1" ]; then
  run gh api -X POST "repos/${GITHUB_REPOSITORY}/pages/builds" >/dev/null 2>&1 || say "aviso: não foi possível pedir a construção do Pages"
fi
say "concluído"
