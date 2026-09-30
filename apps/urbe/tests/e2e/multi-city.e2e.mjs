// E2E (REQ-045, RM-F1-19): instalação 1.x com várias cidades → uma só ("Urbe"), com 3 boots idênticos,
// IDs da origem preservados, mapa gravado só pelo WorkspacePersistence e "arquivar" sem mover dados.
import assert from 'node:assert/strict';
import { launchApp, openApp, readVault, seedVault, waitSaved, waitCityLoaded } from '../../tools/lib/browser.mjs';

const mapaNorte = JSON.stringify({ v: 4, mundo: 'placas-1', regioes: [{ caminho: 'Bairro', nome: 'Bairro', cor: '#4aa3ff', x: 30, y: 30, w: 12, h: 10, cells: null }], notas: { 'Bairro/Um.md': { id: 'doc_norte_1', x: 32, y: 32, sprite: 'house1' } }, construcoes: [] });
const app = await launchApp();
try {
  const h = await app.newPage(), { page } = h;
  await page.goto(h.url + '/manifest.webmanifest');
  await seedVault(page, new Map([['Bairro/Um.md', '# Um\n'], ['.urbe/mapa.json', mapaNorte]]), 'Norte');
  await seedVault(page, new Map([['Dois.md', '# Dois\n']]), 'Sul');
  await page.addInitScript(() => {
    window.__mapaWrites = [];
    const put = IDBObjectStore.prototype.put;
    IDBObjectStore.prototype.put = function (v, k) { if (String(k) === 'Urbe/.urbe/mapa.json') window.__mapaWrites.push(new Error('mapa').stack); return put.apply(this, arguments); };
  });
  const snapshot = async () => { const v = await readVault(page); return JSON.stringify(Object.keys(v).filter((k) => !k.startsWith('.urbe/backup/') && !k.startsWith('Tutorial/')).sort().map((k) => [k, k === '.urbe/vault.json' ? JSON.parse(v[k]).migrations.map((m) => m.id) : k.endsWith('.md') ? v[k] : k === '.urbe/mapa.json' ? JSON.parse(v[k]).regioes.map((r) => r.caminho).sort() : 1])); };
  const boot = async (first) => { if (first) await openApp(h, { docs: 2 }); else { await page.reload(); await page.waitForFunction(() => window.UrbeCore && UrbeCore.state.select('ready') && UrbeCore.service('documents').list().length >= 2, null, { timeout: 60000 }); await waitCityLoaded(page); } await page.evaluate(() => UrbeMultiCity.finishing); await waitSaved(page); return snapshot(); };

  const s1 = await boot(true);
  const docs = await page.evaluate(() => UrbeCore.service('documents').list().filter((d) => d.path.startsWith('Cidades/')).map((d) => [d.path, d.id]));
  assert.deepEqual(Object.fromEntries(docs)['Cidades/Norte/Bairro/Um.md'], 'doc_norte_1', 'ID da cidade de origem preservado');
  assert.ok(docs.some(([p]) => p === 'Cidades/Sul/Dois.md'));
  const v1 = await readVault(page), mapa = JSON.parse(v1['.urbe/mapa.json']);
  assert.ok(mapa.regioes.some((r) => r.caminho === 'Cidades/Norte/Bairro'), 'geometria da origem fundida');
  assert.equal(mapa.notas['Cidades/Norte/Bairro/Um.md'].id, 'doc_norte_1');
  const mig = JSON.parse(v1['.urbe/vault.json']).migrations.filter((m) => m.id === 'multi-city');
  assert.equal(mig.length, 1); assert.deepEqual(mig[0].sources, ['Norte']);
  const s2 = await boot(false), s3 = await boot(false);
  assert.equal(s2, s1, 'boot 2 = boot 1'); assert.equal(s3, s1, 'boot 3 = boot 1');
  const writes = await page.evaluate(() => window.__mapaWrites);
  assert.ok(writes.length >= 1);
  assert.deepEqual(writes.filter((s) => !/persistence\/workspace\.js/.test(s)), [], 'mapa de Urbe gravado só pelo WorkspacePersistence');

  // arquivar: some da lista de cidades, nada apagado
  await page.evaluate(() => UrbeCore.commands.execute('workspace.archiveOldCities'));
  const hidden = await page.evaluate(() => [...UrbeMultiCity.archived(UrbeCore.service('persistence'))].sort());
  assert.deepEqual(hidden, ['Norte', 'Sul']);
  const cities = await page.evaluate(async () => { const db = await new Promise((ok) => { const r = indexedDB.open('knowledge-city', 3); r.onsuccess = () => ok(r.result); }); return await new Promise((ok) => { const t = db.transaction('fs'), q = t.objectStore('fs').getAllKeys(); q.onsuccess = () => ok([...new Set(q.result.map((k) => String(k).split('/')[0]))].sort()); }); });
  assert.deepEqual(cities, ['Norte', 'Sul', 'Urbe'], 'as cidades de origem continuam guardadas');
  assert.deepEqual(h.errors, [], 'sem erros: ' + h.errors.join(' | '));
  console.log('multi-city.e2e: ok');
} finally { await app.close(); }
