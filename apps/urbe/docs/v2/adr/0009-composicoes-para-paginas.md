# ADR-0009 — Composições migram para páginas

- Status: Accepted (aprovado pelo proprietário em 2026-09-30)
- Data: 2026-09-30
- Requisitos: REQ-105, REQ-038, REQ-030
- Decisores: Abner P. S. Cruz (proprietário)

## Contexto

Composições juntam notas em um documento HTML; na prática são páginas. Elas têm dados de usuário (`.urbe/compositions.json`, `compositions.v2.json`) e módulos próprios (`composition/store|compiler|ui`).

## Alternativas consideradas

### A — Migrar para páginas e remover

Uma só superfície (editor de páginas); exige migração com backup.

### B — Manter só leitura

Dados legíveis, sem novas composições; mantém código morto.

### C — Manter

Mantém duas superfícies para o mesmo resultado.

## Decisão

**A.** Cada composição vira uma página (`Páginas/Composições/<nome>.page.json`): `sources` (IDs) viram seções de tipo "nota" na ordem `order`; `theme`, `styles`, `overrides`, `customCSS` e `htmlSource` são mapeados para o tema/estilos da página (o que não mapear é preservado em `meta.legacyComposition`). Migração automática com backup (REQ-038) e idempotente; depois a UI e os módulos de composição são removidos e `compositions*.json` permanecem intactos como cópia de segurança. Vaults sem composições não sofrem nada.

## Migração

Na primeira abertura 2.x com `compositions*.json` não vazio: backup → gerar páginas → marcar em `vault.json.migrations` → remover UI. Reversão: restaurar o backup.

## Validação

Fixture `v1-composicoes-historico` (com `sources`, `order`, `overrides`): a página gerada renderiza o mesmo conteúdo; nenhum dado perdido; E2E (RM-F7-25).

## Relações

- origem: `discovery/FEEDBACK-BETA.md`; documentos afetados: DATA-CATALOG, SPEC §15
