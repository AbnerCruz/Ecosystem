// RM-F2-09 (REQ-027): `renderMarkdown` vive em src/editor/markdown.js. O golden foi gerado a partir do código que ainda estava
// em `src/app.js` (antes da extração) com um corpus de tabelas, callouts, listas, código, frontmatter, wikilinks, links/imagens
// (sanitização), cabeçalho vazio e matemática; a extração não pode mudar nenhuma saída.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import { rd } from '../tools/lib/modules.mjs';

const ctx = { window: {}, console }; ctx.window.window = ctx.window; vm.createContext(ctx);
vm.runInContext(rd('src/math/core.js'), ctx);
vm.runInContext(rd('src/editor/markdown.js'), ctx);
const M = ctx.window.UrbeMarkdown;
assert.ok(M && typeof M.render === 'function', 'UrbeMarkdown.render exposto');

const golden = JSON.parse(fs.readFileSync(new URL('./fixtures/markdown-golden.json', import.meta.url), 'utf8'));
assert.ok(golden.length >= 30, 'corpus do golden');
for (const { input, html } of golden) assert.equal(M.render(input), html, 'saída igual ao golden para: ' + JSON.stringify(input).slice(0, 80));

// comportamentos explícitos (legíveis sem o golden)
assert.match(M.render('| a | b |\n|:--|--:|\n| 1 | 2 |'), /<table data-md-table="1" data-align="left,right">/, 'tabela com alinhamento');
assert.match(M.render('> [!tip] Dica\n> corpo'), /class="callout callout-tip" data-callout="tip"/, 'callout');
assert.match(M.render('- [x] feita'), /<li class="task"><input type="checkbox" checked>/, 'tarefa marcada');
assert.match(M.render('[[Nota|rótulo]]'), /<span class="wikilink" data-note-name="Nota">rótulo<\/span>/, 'wikilink com rótulo');
assert.match(M.render('#'), /<h1><br><\/h1>/, 'cabeçalho vazio vira <h1> editável');
assert.match(M.render('---\ntitle: X\n---\ncorpo'), /data-frontmatter-card="1"/, 'frontmatter vira cartão');

// sanitização: nenhum esquema perigoso sobrevive em href/src
for (const bad of ['javascript:alert(1)', 'JaVaScRiPt:alert(1)', 'vbscript:x', 'file:///etc/passwd', 'data:text/html;base64,AAAA']) {
  const h = M.render(`[a](${bad}) ![b](${bad})`);
  assert.ok(!/(href|src)="(javascript|vbscript|file|data:text)/i.test(h), 'esquema bloqueado: ' + bad);
  assert.equal(M.safeUrl(bad), '#', 'safeUrl: ' + bad);
}
assert.equal(M.safeUrl('data:image/png;base64,AAAA'), 'data:image/png;base64,AAAA', 'imagens data: continuam permitidas');
assert.equal(M.escapeHTML('<&"\'>'), '&lt;&amp;&quot;&#39;&gt;');

// autoridade única: app.js consome o módulo e não define mais o pipeline
const app = rd('src/app.js');
assert.ok(app.includes('window.UrbeMarkdown'), 'app.js consome UrbeMarkdown');
for (const fn of ['renderMarkdown', 'inlineMarkdown', 'splitFrontmatter', 'renderFrontmatter', 'escapeHTML', 'mdTableCells', 'mdIsTableSep', 'v22Url', 'v22Enfase'])
  assert.ok(!new RegExp(`function\\s+${fn}\\s*\\(`).test(app), `${fn}: não é mais definida em app.js`);
assert.ok(!/MD_CALLOUTS\s*=/.test(app) && !/\brenderMarkdown\s*=\s*function/.test(app) && !/\binlineMarkdown\s*=\s*function/.test(app), 'sem reatribuição/camadas em app.js');
console.log('OK   markdown: ' + golden.length + ' casos iguais ao golden; sanitização; autoridade única em src/editor/markdown.js');
