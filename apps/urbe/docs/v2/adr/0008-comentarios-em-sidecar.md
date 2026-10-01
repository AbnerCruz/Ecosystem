# ADR-0008 — Comentários em sidecar fora do arquivo

- Status: Accepted (aprovado pelo proprietário em 2026-09-30)
- Data: 2026-09-30
- Requisitos: REQ-089, REQ-042, REQ-013
- Decisores: Abner P. S. Cruz (proprietário)

## Contexto

O proprietário quer comentar trechos das notas (hoje só existe citação). Os arquivos do usuário são dele e nunca recebem metadados injetados (`app.js:1902-1907`); a identidade documental já é por ID (ADR-0006).

## Alternativas consideradas

### A — Sidecar `.urbe/comments.json` ancorado por ID + trecho

Não altera a nota; exige reancoragem quando o texto muda por fora.

### B — Sintaxe inline no Markdown

Portável e visível em qualquer editor, mas altera o arquivo e polui o texto.

### C — Frontmatter

Altera o arquivo; ruim para muitos comentários.

## Decisão

**A.** `.urbe/comments.json` `{version:1,comments:{<docId>:[{id,anchor:{start,end,quote,fingerprint},text,author,created,modified,resolved}]}}`. A âncora guarda o trecho citado e um fingerprint do contexto; na abertura, se o texto mudou, o comentário é reancorado pela citação (busca por `quote`) ou marcado "órfão" (nunca descartado). Versão desconhecida: preservar (REQ-035). Na exportação para páginas HTML: notas marginais opcionais ou omitidos.

## Migração

Formato novo e aditivo (não há migração). Entra em DATA-CATALOG §9 (política) e nas fixtures.

## Validação

Testes de reancoragem (trecho movido/apagado), round-trip, proteção forward e exportação (RM-F7-13).

## Relações

- origem: `discovery/FEEDBACK-BETA.md`; documentos afetados: DATA-CATALOG, SPEC §15
