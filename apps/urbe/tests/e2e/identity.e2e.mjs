// E2E (REQ-042, RM-F1-15): rename/move feito fora do app mantém ID, casa, região e asset — com o app fechado
// (reconciliação no load) e aberto (syncFromDisk).
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { launchApp, openApp, readVault, waitSaved, waitCityLoaded } from '../../tools/lib/browser.mjs';

const DIR = new URL('../fixtures/vaults/v1-mapa-v4/', import.meta.url).pathname;
const seed = new Map();
(function walk(d, pre) { for (const e of readdirSync(d, { withFileTypes: true })) { if (e.isDirectory()) walk(join(d, e.name), pre + e.name + '/'); else if (e.name !== 'expect.json') { const rel = pre + e.name, buf = readFileSync(join(d, e.name)); seed.set(rel, /\.(md|json)$/.test(rel) ? buf.toString('utf8') : new Uint8Array(buf)); } } })(DIR, '');

// move chaves do IndexedDB do modo interno (é o "gerenciador de arquivos" do vault web)
const moveIdb = (page, moves) => page.evaluate(async (moves) => {
  const db = await new Promise((ok, err) => { const r = indexedDB.open('knowledge-city', 3); r.onsuccess = () => ok(r.result); r.onerror = () => err(r.error); });
  await new Promise((ok, err) => { const t = db.transaction('fs', 'readwrite'), s = t.objectStore('fs');
    for (const [from, to] of moves) { const g = s.get('Urbe/' + from); g.onsuccess = () => { if (g.result === undefined) return; s.put(g.result, 'Urbe/' + to); s.delete('Urbe/' + from); }; }
    t.oncomplete = ok; t.onerror = () => err(t.error); });
  db.close();
}, moves);
const ready = async (page, n) => { await page.waitForFunction((n) => window.UrbeCore && UrbeCore.state.select('ready') && UrbeCore.service('documents').list().length >= n, n, { timeout: 60000 }); await waitCityLoaded(page); };
const house = (page, path) => page.evaluate((path) => { const d = UrbeCore.service('documents').get(path); const b = d && UrbeCore.service('diagnostics.world').legacy().buildings.filter((x) => x.tipo === 'nota' && x.documentId === d.id); return d && { id: d.id, houses: b.length, x: b[0]?.x, y: b[0]?.y }; }, path);

const app = await launchApp();
try {
  const h = await app.newPage({ seed });
  const { page } = h;
  await openApp(h, { docs: 3 });
  await page.evaluate(() => UrbeCore.commands.execute('workspace.save')); await waitSaved(page);
  const mapa0 = JSON.parse((await readVault(page))['.urbe/mapa.json']);
  const alfa0 = await house(page, 'Alfa.md'), gama0 = await house(page, 'Pasta/Gama.md');
  const reg0 = mapa0.regioes.find((r) => r.caminho === 'Pasta'), ast0 = mapa0.construcoes.find((c) => c.name === 'logo.png');
  assert.ok(reg0?.id && ast0?.id);

  // 1) app fechado: renomear nota e pasta por fora, reabrir
  // o service worker precisa ter assumido antes (senão ele assume na próxima visita e o app recarrega sozinho no meio do teste)
  await page.waitForFunction(() => !navigator.serviceWorker || navigator.serviceWorker.controller, null, { timeout: 30000 });
  await page.goto(h.url + '/package.json'); // mesma origem, sem o app rodando
  await moveIdb(page, [['Alfa.md', 'Alfa nova.md'], ['Pasta/Gama.md', 'Projetos/Gama.md'], ['Pasta/logo.png', 'Projetos/logo.png'], ['Pasta/.pasta', 'Projetos/.pasta']]);
  await page.goto(h.url + '/index.html'); await ready(page, 3);
  const alfa1 = await house(page, 'Alfa nova.md'), gama1 = await house(page, 'Projetos/Gama.md');
  assert.equal(alfa1?.id, alfa0.id, 'nota renomeada por fora mantém o ID');
  assert.deepEqual([alfa1.x, alfa1.y, alfa1.houses], [alfa0.x, alfa0.y, 1], 'mesma casa, no mesmo lugar, sem duplicata');
  assert.equal(gama1?.id, gama0.id); assert.deepEqual([gama1.x, gama1.y], [gama0.x, gama0.y]);
  await page.evaluate(() => UrbeCore.commands.execute('workspace.save')); await waitSaved(page);
  const vault = await readVault(page), mapa1 = JSON.parse(vault['.urbe/mapa.json']);
  const reg1 = mapa1.regioes.find((r) => r.id === reg0.id), ast1 = mapa1.construcoes.find((c) => c.id === ast0.id);
  assert.equal(reg1?.caminho, 'Projetos', 'região da pasta renomeada mantém o ID'); assert.deepEqual([reg1.x, reg1.y, reg1.w, reg1.h], [reg0.x, reg0.y, reg0.w, reg0.h]);
  assert.equal(ast1?.files?.[0]?.relPath, 'Projetos/logo.png', 'asset mantém o ID e aponta para o caminho novo');
  assert.equal(ast1.parentId, alfa0.id, 'vínculo asset → nota renomeada intacto');
  assert.ok(vault['Projetos/logo.png']?.blob, 'binário no lugar novo'); assert.equal(vault['Pasta/logo.png'], undefined, 'e não recriado no antigo');
  assert.equal(JSON.parse(vault['.urbe/identity.json']).docs[alfa0.id].path, 'Alfa nova.md');
  assert.ok(vault['Alfa nova.md'] && vault['Projetos/Gama.md'], 'os caminhos novos do usuário permanecem (o mundo não reverte o rename)');
  assert.equal(vault['Alfa.md'], undefined); assert.equal(vault['Pasta/Gama.md'], undefined);

  // 2) app aberto: mover por fora e sincronizar
  const beta0 = await house(page, 'Beta.md');
  await moveIdb(page, [['Beta.md', 'Arquivo/Beta.md']]);
  await page.evaluate(() => UrbeCore.service('persistence').syncFromDisk());
  await page.waitForTimeout(600);
  const beta1 = await house(page, 'Arquivo/Beta.md');
  assert.equal(beta1?.id, beta0.id, 'syncFromDisk: mesmo ID'); assert.equal(beta1.houses, 1, 'uma casa só');
  await page.evaluate(() => UrbeCore.commands.execute('workspace.save')); await waitSaved(page);
  const v2 = await readVault(page);
  assert.ok(v2['Arquivo/Beta.md'], 'move para outra pasta não é revertido'); assert.equal(v2['Beta.md'], undefined);
  const m2 = JSON.parse(v2['.urbe/mapa.json']);
  assert.equal(m2.notas['Arquivo/Beta.md']?.id, beta0.id, 'mapa com o ID no caminho novo');
  assert.deepEqual(h.errors, [], 'sem erros: ' + h.errors.join(' | '));
  console.log('identity.e2e: ok');
} finally { await app.close(); }
