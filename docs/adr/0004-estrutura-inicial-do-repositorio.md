# ADR-0004 — Estrutura inicial do repositório

## Status

Aceito — ratificado pelo proprietário em DEC-0001 ([ADD-0003](../governance/addenda/ADD-0003-aprovacao-de-decisoes-e-superficie-de-decisoes-no-portal.md), 2026-09-30).

## Contexto

MANIFEST §11.1 propõe uma estrutura conceitual (`apps/`, `platform/`, `workspaces/`, `tools/`, `docs/`, `tests/`, `.github/`) e declara que ela é "uma direção inicial, não autorização para criar diretórios vazios sem função". O agente fundador pode refiná-la registrando a decisão. MANIFEST §45 lista o conteúdo mínimo do primeiro commit.

## Problema

Quais diretórios criar na Fase 0 e onde colocar os artefatos da fundação que não aparecem explicitamente em §11.1.

## Opções

1. Criar toda a árvore de §11.1 com placeholders.
2. Criar apenas o que tem conteúdo com função; manter §11.1 como direção para os próximos diretórios.

## Decisão

Opção 2.

- Criados: `docs/adr/`, `docs/contracts/` (com `schemas/`), `docs/governance/` (com `handoffs/`), `docs/migration/`, `tests/consistency/`, `.github/`.
- **Não** criados ainda: `apps/`, `platform/`, `workspaces/`, `tools/`, `docs/architecture/`, `tests/architecture/`, `tests/contracts/`, `tests/integration/`. Cada um nasce com o primeiro conteúdo real (ex.: `apps/lunet2d/` na importação do Lunet2D).
- Os paths-alvo dos produtos (`apps/lunet2d`, `apps/urbe`, `apps/hub`) já estão declarados em `ecosystem.json`; `CHK-SINGLE-AUTHORITY` impede que existam enquanto o produto estiver `planned`/`not-migrated`.
- Os checks da fundação ficam em `tests/consistency/` (são testes do repositório, não Tools).
- Schemas dos registros ficam em `docs/contracts/schemas/`.

## Consequências

- Nenhum diretório vazio ou placeholder sem função; o repositório reflete o estado real (NN-021).
- A árvore de §11.1 continua sendo a direção; desvios futuros exigem ADR.

## Alternativas rejeitadas

- **Árvore completa com placeholders:** contraria §11.1, §45 e §47 e sugeriria componentes que não existem.

## Referências

MANIFEST §11.1, §45, §47, §57.16; NN-021.
