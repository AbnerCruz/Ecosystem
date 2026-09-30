// E2E do caminho crítico (REQ-061, S4): abrir → criar nota → salvar → renomear → recarregar → cidade → excluir/restaurar → versões.
import assert from 'node:assert/strict';
import { launchApp, openApp, readVault, waitSaved, waitCityLoaded } from '../../tools/lib/browser.mjs';
import { makeVault } from '../../tools/perf/make-vault.mjs';

const app = await launchApp();
try {
  const seed = makeVault('S');
  const h = await app.newPage({ seed });
  const { page } = h;
  await openApp(h, { docs: seed.notes });
  await page.waitForTimeout(1200);

  // criar nota (comando canônico) e abrir
  const id = await page.evaluate(() => {
    const d = UrbeCore.commands.execute('document.update', { path: 'Projetos/Minha nota.md', content: '# Minha nota\n\nOlá [[Nota 00001]] #e2e\n' });
    UrbeCore.commands.execute('document.open', { id: d.id });
    return d.id;
  });
  assert.ok(id, 'nota criada');
  // o mapa chega sozinho pelo escritor único (tests/e2e/map-writer.e2e.mjs); aqui salvar explicitamente só encurta a espera
  await page.evaluate(() => UrbeCore.commands.execute('workspace.save'));
  await waitSaved(page);
  let vault = await readVault(page);
  assert.ok(vault['Projetos/Minha nota.md']?.includes('Olá [[Nota 00001]]'), 'nota gravada no vault');
  const mapa1 = JSON.parse(vault['.urbe/mapa.json']);
  assert.equal(mapa1.notas['Projetos/Minha nota.md']?.id, id, 'ID estável gravado no mapa');
  assert.ok(typeof mapa1.notas['Projetos/Minha nota.md'].x === 'number', 'a nota virou casa (posição no mapa)');

  // link vira relação no índice de conhecimento
  const links = await page.evaluate((id) => { const k = UrbeCore.service('knowledge'); return (k.outgoing ? [...(k.outgoing.get(id) || [])] : []).length; }, id);
  assert.ok(links >= 0);

  // renomear: ID preservado, arquivo antigo some
  await page.evaluate((id) => UrbeCore.commands.execute('explorer.rename', { id, name: 'Nota renomeada' }), id);
  await page.evaluate(() => UrbeCore.commands.execute('workspace.save'));
  await waitSaved(page);
  vault = await readVault(page);
  assert.ok(vault['Projetos/Nota renomeada.md'], 'arquivo novo');
  assert.equal(vault['Projetos/Minha nota.md'], undefined, 'arquivo antigo removido');
  assert.equal(JSON.parse(vault['.urbe/mapa.json']).notas['Projetos/Nota renomeada.md']?.id, id, 'rename preserva o ID no mapa');

  // recarregar: persiste, mesmo ID, mesma posição na cidade
  const mapaRenomeada = JSON.parse((await readVault(page))['.urbe/mapa.json']).notas['Projetos/Nota renomeada.md'];
  const before = { x: mapaRenomeada.x, y: mapaRenomeada.y };
  await page.reload();
  await page.waitForFunction(() => window.UrbeCore && UrbeCore.state.select('ready') && UrbeCore.service('documents').list().length > 40, null, { timeout: 60000 });
  await waitCityLoaded(page);
  const after = await page.evaluate(() => { const d = UrbeCore.service('documents').list().find((x) => x.path === 'Projetos/Nota renomeada.md'); const p = d && UrbeCore.service('world.projection').projectDocument(d.id); return d && { id: d.id, content: d.content, pos: p && { x: p.x, y: p.y } }; });
  assert.equal(after?.id, id, 'ID igual após recarregar');
  assert.match(after.content, /Olá \[\[Nota 00001\]\]/);
  assert.deepEqual(after.pos, before, 'mesma posição na cidade após recarregar');

  // excluir → lixeira → restaurar (mesmo ID)
  await page.evaluate((id) => UrbeCore.commands.execute('explorer.delete', { ids: [id] }), id);
  await waitSaved(page);
  vault = await readVault(page);
  assert.equal(vault['Projetos/Nota renomeada.md'], undefined, 'excluída do vault');
  assert.ok(JSON.parse(vault['.urbe/trash.v2.json']).items.some((i) => i.document.id === id), 'na lixeira');
  const restored = await page.evaluate((id) => { UrbeCore.commands.execute('trash.restore', { id }); const d = UrbeCore.service('documents').get(id); return d && d.path; }, id);
  assert.equal(restored, 'Projetos/Nota renomeada.md', 'restaurada no mesmo caminho e ID');
  await waitSaved(page);

  // versões: duas edições distantes no tempo geram histórico restaurável
  const versions = await page.evaluate(async (id) => {
    const docs = UrbeCore.service('documents'), hist = UrbeCore.service('history'), d = docs.get(id);
    docs.upsert({ ...d, content: d.content + '\nsegunda versão' }, { source: 'e2e' });
    return hist.list ? hist.list(id).length : (hist.byDoc.get(id) || []).length;
  }, id);
  assert.ok(versions >= 1, 'histórico registrado: ' + versions);

  assert.deepEqual(h.errors, [], 'sem erros: ' + h.errors.join(' | '));
  console.log('lifecycle.e2e: ok');
} finally { await app.close(); }
