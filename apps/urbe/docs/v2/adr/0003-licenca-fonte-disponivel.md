# ADR-0003 — Licença: todos os direitos reservados

- Status: Accepted (aprovado pelo proprietário em 2026-09-30)
- Data: 2026-09-30
- Requisitos: REQ-017, REQ-080
- Decisores: Abner P. S. Cruz (proprietário)

## Contexto

Repositório público com `package.json` `UNLICENSED` e sem `LICENSE` (AUD-016). Há dependências embutidas com licenças próprias (KaTeX MIT, JSZip MIT/GPLv3, pako MIT, PDF.js Apache-2.0 por CDN).

## Forças

- clareza de direitos para colaboração e distribuição;
- compatibilidade com dependências embutidas;
- intenção do proprietário de manter o código como propriedade sua.

## Alternativas consideradas

### A — MIT

Permissiva; abre mão de controle.

### B — Fonte-disponível/proprietária

Código legível; direitos reservados com permissões explícitas.

### C — GPL/AGPL

Copyleft; incompatível com a intenção declarada.

### D — Adiar

Mantém ambiguidade.

## Decisão

Adotar **B**, na forma de **todos os direitos reservados** (decisão do proprietário, 2026-09-30: "tudo meu"). Criar `LICENSE` de direitos reservados em nome de Abner P. S. Cruz, sem concessão a terceiros; `THIRD-PARTY-NOTICES` (JSZip sob MIT, KaTeX MIT, pako MIT, PDF.js Apache-2.0, Electron/Chromium e Capacitor) e `package.json` coerente. Sem pendência residual.

## Consequências positivas

- ambiguidade removida
- compatível com as dependências

## Consequências negativas / trade-offs

- contribuições externas exigem termos (CLA) a definir

## Migração

Nenhuma migração de dados.

## Validação

`tools/check-license.mjs`: existência de `LICENSE`, `THIRD-PARTY-NOTICES`, coerência com `package.json` (G4).

## Relações

- documentos afetados: README, CONTRIBUTING, PLATFORMS §4
