// E2E: roteamento de abertura por tipo de artefato (REQ-039): páginas abrem no Studio; notas e código no editor; [[link]] não aponta para plugin.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { launchApp, openApp } from '../../tools/lib/browser.mjs';

const DIR = new URL('../fixtures/vaults/v1-paginas/', import.meta.url).pathname;
const pageJson = readFileSync(DIR + 'Páginas/Inicio.page.json', 'utf8');
const app = await launchApp();
try {
  const h = await app.newPage();
  await openApp(h, { docs: 40 });
  const r = await h.page.evaluate((pageJson) => {
    const docs = UrbeCore.service('documents');
    const page = docs.upsert({ path: 'Páginas/Inicio.page.json', content: pageJson });
    const note = docs.upsert({ path: 'Cronometro.md', content: '# Cronometro\n' });
    const plugin = docs.upsert({ path: 'Personalização/plugins/cronometro.js', content: "urbe.plugin({nome:'Cronômetro',versao:'1',ligar:function(){}});\n" });
    const studioVisible = () => { const s = document.getElementById('pageStudio'); return !!s && !s.hidden; };
    UrbeCore.commands.execute('document.open', { id: page.id });
    const afterPage = studioVisible();
    UrbeCore.commands.execute('document.open', { id: note.id });
    const afterNote = studioVisible();
    UrbeCore.commands.execute('document.open', { id: page.id, raw: true });
    const afterRaw = studioVisible();
    // [[Cronometro]] resolve para a nota, nunca para o plugin homônimo
    const k = UrbeCore.service('knowledge'), ids = [...(k.byTitle.get('cronometro') || [])];
    return { afterPage, afterNote, afterRaw, linkTargets: ids.map((id) => docs.get(id).path) };
  }, pageJson);
  assert.equal(r.afterPage, true, 'página abre no Studio');
  assert.equal(r.afterNote, false, 'abrir nota fecha o Studio');
  assert.equal(r.afterRaw, false, 'raw abre no editor de texto');
  assert.deepEqual(r.linkTargets, ['Cronometro.md'], 'plugin não é alvo de [[link]]: ' + r.linkTargets);
  assert.deepEqual(h.errors, [], 'sem erros: ' + h.errors.join(' | '));
  console.log('routing.e2e: ok');
} finally { await app.close(); }
