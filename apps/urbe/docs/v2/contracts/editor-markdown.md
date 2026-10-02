# Contrato `UrbeMarkdown` v1

Pipeline de Markdown → HTML do editor Visual, em `src/editor/markdown.js`. Item do ROADMAP: RM-F2-09 (REQ-027). Verificado por
`tests/markdown.mjs` (golden gerado do código que ainda estava em `src/app.js`) e por `tests/e2e/app-runtime.e2e.mjs` (preview no app real).

## 1. Como se obtém

`window.UrbeMarkdown` (global) e o serviço do core `editor.markdown` (`UrbeCore.service('editor.markdown')`). O módulo é carregado
antes de `src/app.js`; `app.js` desestrutura o global no topo do seu IIFE.

## 2. API

| Membro | Contrato |
|---|---|
| `render(md)` | Markdown → HTML. Puro: sem DOM, sem estado, sem I/O. Ordem do pipeline: matemática (`UrbeMath.renderWith`, quando existe) → cabeçalho vazio (`# ` vira `<h1>` editável) → blocos. Nunca lança por entrada de texto. Entrada vazia devolve `<p><br></p>`. |
| `inlineMarkdown(s)` | Trecho inline: wikilinks (`<span class="wikilink" data-note-name>`), imagens, links (`target="_blank" rel="noopener"`), código, ênfase. Escapa HTML antes de qualquer conversão. |
| `splitFrontmatter(md)` | `{ raw, body, fields }`; frontmatter só se o texto começa com `---\n` e fecha com `\n---`. |
| `renderFrontmatter(fields)` | Cartão `data-frontmatter-card` (chips para `tags`/`aliases`). |
| `safeUrl(u)` | Esquemas `javascript:`, `vbscript:`, `file:` e `data:` que não seja `data:image/` viram `#`. |
| `escapeHTML(s)` | Escapa `& < > " '`. |
| `callouts` | Mapa tipo → título padrão dos callouts. |

## 3. Invariantes

- **Segurança (REQ-056):** nenhum `href`/`src` com esquema perigoso sai do pipeline; HTML do texto é sempre escapado.
- **Ida e volta:** tudo o que é desenhado aqui volta igual em `markdownFromVisual` (ainda em `app.js` até RM-F2-10).
- **Uma autoridade:** `app.js` não define mais `renderMarkdown`, `inlineMarkdown`, `splitFrontmatter`, `renderFrontmatter`, `escapeHTML`, tabelas, callouts nem sanitização de URL (`tests/markdown.mjs` falha se voltarem).
- **Compatibilidade:** mudar uma saída exige atualizar o golden de propósito (a diferença aparece no diff do PR); o formato dos arquivos do usuário (`.md`) não muda.
