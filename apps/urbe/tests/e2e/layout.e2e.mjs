// E2E (REQ-043, RM-F1-17): reorganizar a cidade (mundo antigo, mapa sem `mundo`, comando) sempre faz backup do mapa,
// registra em vault.json e pode ser desfeito; desfazer é persistido e não reorganiza de novo na próxima abertura.
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { launchApp, openApp, readVault, waitSaved, waitCityLoaded } from '../../tools/lib/browser.mjs';

const FIX = new URL('../fixtures/vaults/', import.meta.url).pathname;
function fixture(name) { const out = new Map(); (function walk(d, pre) { for (const e of readdirSync(d, { withFileTypes: true })) { if (e.isDirectory()) walk(join(d, e.name), pre + e.name + '/'); else if (e.name !== 'expect.json') { const rel = pre + e.name, buf = readFileSync(join(d, e.name)); out.set(rel, /\.(md|json)$/.test(rel) ? buf.toString('utf8') : new Uint8Array(buf)); } } })(join(FIX, name), ''); return out; }
const pos = (page, path) => page.evaluate((path) => { const d = UrbeCore.service('documents').get(path), b = URBE.mundo.buildings.find((x) => x.tipo === 'nota' && x.documentId === d.id); return [b.x, b.y]; }, path);
const save = async (page) => { await page.evaluate(() => UrbeCore.commands.execute('workspace.save')); await waitSaved(page); return readVault(page); };
const layoutBackups = (vault) => Object.keys(vault).filter((k) => /^\.urbe\/backup\/[^/]+-layout-layout[^/]*\/manifest\.json$/.test(k)).map((k) => JSON.parse(vault[k]));

const app = await launchApp();
try {
  // 1) mundo antigo: reorganiza ao abrir, com backup; "Desfazer" volta as posições e fica gravado
  {
    const seed = fixture('v1-mundo-antigo'), mapa0 = JSON.parse(seed.get('.urbe/mapa.json'));
    const h = await app.newPage({ seed }), { page } = h;
    await openApp(h, { docs: 2 });
    await page.waitForSelector('.udlg [data-primary]');
    assert.match(await page.textContent('.udlg-msg'), /placas-0/);
    const moved = await pos(page, 'Alfa.md');
    assert.notDeepEqual(moved, [mapa0.notas['Alfa.md'].x, mapa0.notas['Alfa.md'].y], 'a cidade foi reorganizada');
    await page.click('.udlg [data-primary]'); // Desfazer
    assert.deepEqual(await pos(page, 'Alfa.md'), [mapa0.notas['Alfa.md'].x, mapa0.notas['Alfa.md'].y], 'desfazer volta a casa');
    let vault = await save(page);
    const b = layoutBackups(vault);
    assert.equal(b.length, 1, 'backup do mapa antes de reorganizar'); assert.equal(b[0].reason, 'mundo');
    const dir = Object.keys(vault).find((k) => k.endsWith('-layout-layout/manifest.json')).replace('/manifest.json', '');
    assert.equal(vault[dir + '/files/urbe/mapa.json'], seed.get('.urbe/mapa.json'), 'o backup é o mapa original, byte a byte');
    const mapa1 = JSON.parse(vault['.urbe/mapa.json']);
    assert.deepEqual([mapa1.notas['Alfa.md'].x, mapa1.notas['Alfa.md'].y], [mapa0.notas['Alfa.md'].x, mapa0.notas['Alfa.md'].y]);
    assert.equal(mapa1.mundo, 'placas-1', 'o mapa passa a declarar o mundo atual');
    const log = JSON.parse(vault['.urbe/vault.json']).maintenance.filter((m) => m.kind === 'layout');
    assert.deepEqual([log[0].reason, log[0].from, log[0].to], ['mundo', 'placas-0', 'placas-1']);
    // reabrir: não reorganiza de novo
    await page.reload();
    await page.waitForFunction(() => window.UrbeCore && UrbeCore.state.select('ready') && UrbeCore.service('documents').list().length >= 2, null, { timeout: 60000 });
    await waitCityLoaded(page);
    assert.equal(await page.$('.udlg [data-primary]'), null, 'sem nova reorganização');
    assert.deepEqual(await pos(page, 'Alfa.md'), [mapa0.notas['Alfa.md'].x, mapa0.notas['Alfa.md'].y]);
    assert.deepEqual(h.errors, [], 'sem erros: ' + h.errors.join(' | '));
  }
  // 2) mapa sem `mundo` (mapa v2 / vault mesclado): mesma regra; "Manter a nova" mantém, com backup e registro
  {
    const h = await app.newPage({ seed: fixture('v1-mapa-v2') }), { page } = h;
    await openApp(h, { docs: 1 });
    await page.waitForSelector('.udlg [data-cancel].ui-btn');
    assert.match(await page.textContent('.udlg-msg'), /sem versão/);
    await page.click('.udlg [data-cancel].ui-btn'); // Manter a nova
    const vault = await save(page);
    assert.equal(layoutBackups(vault).length, 1);
    assert.equal(JSON.parse(vault['.urbe/mapa.json']).mundo, 'placas-1');
    assert.deepEqual(h.errors, [], 'sem erros: ' + h.errors.join(' | '));
  }
  // 3) comando "Organizar os bairros": backup + "Desfazer reorganização"
  {
    const h = await app.newPage({ seed: fixture('v1-mapa-v4') }), { page } = h;
    await openApp(h, { docs: 3 });
    await save(page);
    const before = await pos(page, 'Pasta/Gama.md');
    assert.equal(await page.evaluate(() => UrbeCore.commands.list().find((c) => c.id === 'city.undoReorganize').enabled()), false, 'nada a desfazer ainda');
    await page.evaluate(() => { window.__r = UrbeCore.commands.execute('city.reorganize'); });
    await page.click('.udlg [data-primary]'); await page.evaluate(() => window.__r);
    const after = await pos(page, 'Pasta/Gama.md');
    await page.evaluate(() => UrbeCore.commands.execute('city.undoReorganize'));
    assert.deepEqual(await pos(page, 'Pasta/Gama.md'), before, 'desfazer volta a posição');
    const vault = await save(page);
    const b = layoutBackups(vault); assert.equal(b.length, 1); assert.equal(b[0].reason, 'reorganizar');
    const m = JSON.parse(vault['.urbe/mapa.json']).notas['Pasta/Gama.md']; assert.deepEqual([m.x, m.y], before);
    assert.ok(after, 'reorganizou antes de desfazer');
    assert.deepEqual(h.errors, [], 'sem erros: ' + h.errors.join(' | '));
  }
  console.log('layout.e2e: ok');
} finally { await app.close(); }
