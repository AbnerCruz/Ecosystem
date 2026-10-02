// RM-F2-10 (parte pura, REQ-027): src/editor/visual.js serializa o DOM do editor Visual igual ao código que estava em src/app.js.
// O golden (tests/fixtures/visual-golden.json) foi gerado em Chromium com o código ANTIGO sobre o DOM do render (corpus do Markdown)
// e sobre HTML típico de contenteditable, com e sem frontmatter. O nome do arquivo casa com o filtro `app-runtime` do job e2e do CI.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { startRuntime } from './app-runtime.mjs';

const golden = JSON.parse(readFileSync(new URL('../fixtures/visual-golden.json', import.meta.url), 'utf8'));
assert.ok(golden.length >= 100, 'golden carregado');
const rt = await startRuntime();
try {
  const a = await rt.open({ docs: 40 });

  // 1) núcleo: mesma saída do código antigo em todos os casos
  const diffs = await a.page.evaluate((golden) => golden.map((g, i) => {
    const tpl = document.createElement('template'); tpl.innerHTML = g.html; // documento inerte: <img src> do corpus não faz requisição
    const md = window.UrbeVisual.markdownFromVisual(tpl.content, { frontmatter: window.UrbeMarkdown.splitFrontmatter(g.bodyEditor).raw });
    return md === g.md ? null : { i, esperado: g.md, obtido: md, html: g.html };
  }).filter(Boolean), golden);
  assert.deepEqual(diffs, [], 'saída igual ao golden; diferenças: ' + JSON.stringify(diffs.slice(0, 2)));

  // 2) caminho real do app (adaptador `markdownFromVisual` em app.js): editar o DOM do editor Visual, que já abre em modo visual com
  //    uma nota carregada, grava o Markdown da nota aberta e preserva o que o Visual não mostra (aqui, o resto do texto)
  await a.page.evaluate(() => { const d = UrbeCore.service('documents').list().find((x) => /^# Comece aqui/.test(x.content)); UrbeCore.commands.execute('document.open', { id: d.id }); });
  await a.page.waitForFunction(() => /^# Comece aqui/.test(document.getElementById('bodyEditor').value) && document.getElementById('editorFull').classList.contains('open'), null, { timeout: 15000 });
  await a.page.evaluate(() => {
    const el = document.getElementById('renderedPreview'), p = document.createElement('p');
    p.innerHTML = 'linha nova <strong>forte</strong>'; el.appendChild(p);
    el.dispatchEvent(new Event('input', { bubbles: true }));
  });
  await a.page.waitForFunction(() => /linha nova/.test(document.getElementById('bodyEditor').value), null, { timeout: 10000 });
  await a.save();
  const doc = Object.values(await a.notes()).find((n) => /linha nova/.test(n.content));
  assert.ok(doc, 'a nota aberta recebeu a edição visual');
  assert.match(doc.content, /^# Comece aqui/, 'conteúdo anterior preservado');
  assert.match(doc.content, /linha nova \*\*forte\*\*\n$/, 'edição visual serializada para Markdown');
  a.expectNoErrors();
  console.log('app-runtime-visual.e2e: ok (' + golden.length + ' casos)');
} finally { await rt.close(); }
