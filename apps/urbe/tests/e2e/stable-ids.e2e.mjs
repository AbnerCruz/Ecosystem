// E2E (REQ-041, RM-F1-14): vault 1.x sem IDs de região/asset abre, ganha `reg_`/`ast_` determinísticos e `parentId`,
// mantém `parentNoteName` para a 1.x, e os IDs sobrevivem a recarregar e a renomear a pasta dentro do app.
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { launchApp, openApp, readVault, waitSaved, waitCityLoaded } from '../../tools/lib/browser.mjs';

const DIR = new URL('../fixtures/vaults/v1-mapa-v4/', import.meta.url).pathname;
const TEXT = /\.(md|json)$/i;
const seed = new Map();
(function walk(d, pre) { for (const e of readdirSync(d, { withFileTypes: true })) { if (e.isDirectory()) walk(join(d, e.name), pre + e.name + '/'); else if (e.name !== 'expect.json') { const rel = pre + e.name, buf = readFileSync(join(d, e.name)); seed.set(rel, TEXT.test(rel) ? buf.toString('utf8') : new Uint8Array(buf)); } } })(DIR, '');
const save = async (page) => { await page.evaluate(() => UrbeCore.commands.execute('workspace.save')); await waitSaved(page); return JSON.parse((await readVault(page))['.urbe/mapa.json']); };

const app = await launchApp();
try {
  const h = await app.newPage({ seed });
  const { page } = h;
  await openApp(h, { docs: 3 });
  let mapa = await save(page);
  const reg = mapa.regioes.find((r) => r.caminho === 'Pasta'), ast = mapa.construcoes.find((c) => c.name === 'logo.png');
  assert.match(reg?.id || '', /^reg_/, 'região ganhou ID'); assert.match(ast?.id || '', /^ast_/, 'asset ganhou ID');
  const alfa = await page.evaluate(() => UrbeCore.service('documents').get('Alfa.md').id);
  assert.equal(ast.parentId, alfa, 'vínculo asset → nota por ID do documento');
  assert.equal(ast.parentNoteName, 'Alfa', 'parentNoteName mantido para a 1.x');
  for (const k of ['caminho', 'nome', 'x', 'y', 'w', 'h']) assert.ok(k in reg, 'região mantém ' + k + ' (leitura 1.8.2)');

  // recarregar: mesmos IDs
  await page.reload();
  await page.waitForFunction(() => window.UrbeCore && UrbeCore.state.select('ready') && UrbeCore.service('documents').list().length >= 3, null, { timeout: 60000 });
  await waitCityLoaded(page);
  mapa = await save(page);
  assert.equal(mapa.regioes.find((r) => r.caminho === 'Pasta')?.id, reg.id, 'ID da região estável após recarregar');
  assert.equal(mapa.construcoes.find((c) => c.name === 'logo.png')?.id, ast.id, 'ID do asset estável após recarregar');

  // renomear a pasta no app: caminho muda, ID fica
  await page.evaluate(() => { const r = URBE.mundo.regions.find((x) => x.name === 'Pasta'); r.name = 'Pasta nova'; });
  mapa = await save(page);
  const renomeada = mapa.regioes.find((r) => r.id === reg.id);
  assert.equal(renomeada?.caminho, 'Pasta nova', 'mesmo ID com o caminho novo');
  assert.equal(mapa.construcoes.find((c) => c.id === ast.id)?.parentId, alfa, 'vínculo do asset intacto');
  assert.deepEqual(h.errors, [], 'sem erros: ' + h.errors.join(' | '));
  console.log('stable-ids.e2e: ok');
} finally { await app.close(); }
