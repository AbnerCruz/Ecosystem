# Contrato `UrbeVisual` v1 (parte pura)

Serializador do editor Visual em `src/editor/visual.js`. Item do ROADMAP: RM-F2-10, **primeira fatia** (REQ-027). Verificado por
`tests/e2e/app-runtime-visual.e2e.mjs` (golden em `tests/fixtures/visual-golden.json`, gerado em Chromium com o código que estava em `src/app.js`).

## 1. Como se obtém

`window.UrbeVisual` e o serviço do core `editor.visual`. Carregado antes de `src/app.js`; `app.js` mantém um adaptador de uma linha,
`markdownFromVisual(root)`, que injeta o frontmatter do texto-fonte.

## 2. API

| Membro | Contrato |
|---|---|
| `markdownFromVisual(root, opts)` | DOM do editor Visual (um `Element` ou `DocumentFragment`) → Markdown. `opts.frontmatter` é o texto bruto do frontmatter (o Visual não o mostra); é reposto no início. Camadas, nesta ordem: núcleo → normalização (remove U+200B; `#` vazio mantém o espaço) → matemática (`UrbeMathEditor.serialize`, quando existe). |

## 3. Invariantes e limites

- É o inverso **parcial** de `UrbeMarkdown.render`: no corpus de 33 entradas só 11 voltam byte a byte iguais (as outras são normalizadas, por exemplo `*itálico*` volta como `_itálico_`). Isso é o comportamento herdado e está travado pelo golden; mudar uma saída exige atualizar o golden de propósito.
- Sem estado e sem I/O. O que ainda está em `app.js` (RM-F2-10, restante): `setEditorViewMode`, sincronização com o campo-fonte, seleção/cursor, estado vazio e as camadas que os embrulham.
