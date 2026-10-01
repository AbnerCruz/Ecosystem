// E2E (REQ-040, RM-F1-13): `.urbe/mapa.json` tem um único escritor, WorkspacePersistence; mudanças feitas só pela API
// do core (sem o `workspace.save` legado) chegam ao mapa; nenhuma gravação do mapa parte de outro caminho.
import assert from 'node:assert/strict';
import { launchApp, openApp, readVault, waitSaved } from '../../tools/lib/browser.mjs';
import { makeVault } from '../../tools/perf/make-vault.mjs';

const app = await launchApp();
try {
  const seed = makeVault('S');
  const h = await app.newPage({ seed });
  const { page } = h;
  await openApp(h, { docs: seed.notes });
  // registra a pilha (inclui frames async) de toda gravação de mapa.json no IndexedDB
  await page.evaluate(() => {
    window.__mapaWrites = [];
    const put = IDBObjectStore.prototype.put;
    IDBObjectStore.prototype.put = function (value, key) {
      if (String(key).endsWith('/.urbe/mapa.json')) window.__mapaWrites.push(new Error('mapa').stack);
      return put.apply(this, arguments);
    };
  });

  // 1) nota criada só pela API do core: vira casa e entra no mapa sem workspace.save
  const id = await page.evaluate(() => UrbeCore.commands.execute('document.update', { path: 'Projetos/Só API.md', content: '# Só API\n' }).id);
  await page.waitForFunction((id) => { const b = (window.UrbeCore.service('world.projection').projectDocument(id)); return !!b; }, id, { timeout: 10000 }).catch(() => {});
  await page.waitForTimeout(1500); await waitSaved(page);
  let vault = await readVault(page);
  const nota = JSON.parse(vault['.urbe/mapa.json']).notas['Projetos/Só API.md'];
  assert.ok(nota, 'nota criada pela API está no mapa');
  assert.equal(nota.id, id, 'com o ID estável');

  // 2) mudança só de mundo (reorganizar a cidade) também chega ao mapa
  const antes = vault['.urbe/mapa.json'];
  await page.evaluate(() => { window.__reorg = UrbeCore.commands.execute('city.reorganize'); });
  await page.click('.udlg [data-primary]');
  await page.evaluate(() => window.__reorg);
  await page.waitForTimeout(1500); await waitSaved(page);
  vault = await readVault(page);
  assert.notEqual(vault['.urbe/mapa.json'], antes, 'reorganizar grava o mapa');
  assert.equal(JSON.parse(vault['.urbe/mapa.json']).v, 4);

  // 3) todas as gravações do mapa vieram do WorkspacePersistence
  const writes = await page.evaluate(() => window.__mapaWrites);
  assert.ok(writes.length >= 2, 'houve gravações do mapa: ' + writes.length);
  const outros = writes.filter((s) => !/persistence\/workspace\.js/.test(s));
  assert.deepEqual(outros, [], 'mapa.json gravado fora do WorkspacePersistence');
  assert.deepEqual(h.errors, [], 'sem erros: ' + h.errors.join(' | '));
  console.log('map-writer.e2e: ok (' + writes.length + ' gravações, todas pelo WorkspacePersistence)');
} finally { await app.close(); }
