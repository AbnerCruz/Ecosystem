# ADR-0005 — Release por tag e fonte única de versão

- Status: Accepted (aprovado pelo proprietário em 2026-09-30)
- Data: 2026-09-30
- Requisitos: REQ-006, REQ-019, REQ-065, REQ-066, REQ-081
- Decisores: Abner P. S. Cruz (proprietário)

## Contexto

`app.yml` publica Release em todo push a `main` com versão inédita (AUD-006); não depende de `structural-checks`. A versão está em 10 lugares (ARCHITECTURE-MAP §6) e `V21_VERSION` é atribuída 4×.

## Forças

- integração em `main` não deve equivaler a publicação;
- Electron e Android derivam versão de `package.json`;
- release notes vêm do CHANGELOG.

## Alternativas consideradas

### A — Tag `v*` (protegida) ou `workflow_dispatch`, com `needs` nos testes

Separa integração e publicação; um passo manual a mais.

### B — Branch `release/*`

Estabiliza, mas adiciona fluxo de branches.

### C — Manter acoplamento com aprovação de ambiente

Menor mudança; publicação continua atrelada ao merge.

## Decisão

Recomendação: **A**. Fonte única de versão em `package.json`; `tools/version.mjs check|sync` valida e sincroniza `core.js`, `index.html`, `sw.js`, CHANGELOG e testes; `V21_VERSION` deixa de ser atribuída em camadas. O workflow de release roda em tag `v*` ou `workflow_dispatch`, depende de testes e gates de dados verdes, usa `permissions` mínimas e actions fixadas por SHA; Dependabot ativo. SemVer com canal (`2.0.0-beta.N`, `2.0.0`). Rollback: marcar Release como pre-release/removê-lo e republicar o `latest.yml` anterior; dados por backup de migração.

## Aditamento de distribuição no monorepo (2026-10-03)

ADD-0015 / DEC-0031 / ADR-0019 do Ecosystem, decisão posterior e explícita do proprietário, preserva esta decisão e especializa o nome da tag no monorepo: o canal legado continua usando `v<versão>` para a versão-ponte; releases diretas do Product no repositório Ecosystem usam `urbe-v<versão>`. A publicação continua deliberada e separada da integração. O updater nunca usa o `latest` global do monorepo e a assinatura Android não pode mudar.

## Consequências positivas

- publicação deliberada e rastreável
- drift de versão impedido por gate

## Consequências negativas / trade-offs

- exige criar tag para publicar

## Migração

Primeiro `version.mjs`; depois separar o workflow; por fim proteger tags. Nenhuma mudança de runtime.

## Validação

CI: `version.mjs check`; dry-run do workflow de release; teste de que push em `main` não publica (G4).

## Relações

- documentos afetados: CONTRIBUTING, PLATFORMS §5, `.github/workflows/*` (na execução)
