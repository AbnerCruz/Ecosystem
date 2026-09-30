// E2E (REQ-042, RM-F1-16): "Limpar referências órfãs" mostra o que achou, só aplica com confirmação e não toca dados ativos.
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { launchApp, openApp, readVault, waitSaved } from '../../tools/lib/browser.mjs';

const DIR = new URL('../fixtures/vaults/v1-orfaos/', import.meta.url).pathname;
const E = JSON.parse(readFileSync(join(DIR, 'expect.json'), 'utf8'));
const seed = new Map();
(function walk(d, pre) { for (const e of readdirSync(d, { withFileTypes: true })) { if (e.isDirectory()) walk(join(d, e.name), pre + e.name + '/'); else if (e.name !== 'expect.json') seed.set(pre + e.name, readFileSync(join(d, e.name), 'utf8')); } })(DIR, '');

const app = await launchApp();
try {
  const h = await app.newPage({ seed });
  const { page } = h;
  await openApp(h, { docs: 2 });
  // a API crua não aparece na paleta; a versão com diálogo sim
  const palette = await page.evaluate(() => UrbeCore.commands.list().filter((c) => c.enabled({ source: 'palette' })).map((c) => c.id));
  assert.ok(palette.includes('workspace.cleanOrphans') && !palette.includes('workspace.gc'));

  // cancelar não muda nada
  await page.evaluate(() => { window.__gc = UrbeCore.commands.execute('workspace.cleanOrphans', { source: 'palette' }); });
  await page.waitForSelector('.udlg [data-primary]');
  const msg = await page.textContent('.udlg-msg');
  assert.match(msg, /histórico/); assert.match(msg, /composição/);
  await page.click('.udlg [data-cancel].ui-btn');
  await page.evaluate(() => window.__gc); await waitSaved(page);
  let hist = await page.evaluate(() => Object.keys(UrbeCore.service('history').export().documents));
  assert.ok(hist.includes(E.orphans.historyOld[0]), 'cancelar mantém tudo');

  // confirmar aplica
  await page.evaluate(() => { window.__gc = UrbeCore.commands.execute('workspace.cleanOrphans', { source: 'palette' }); });
  await page.click('.udlg [data-primary]');
  await page.evaluate(() => window.__gc); await waitSaved(page);
  const vault = await readVault(page);
  hist = Object.keys(JSON.parse(vault['.urbe/history.v2.json']).documents);
  assert.ok(!hist.includes(E.orphans.historyOld[0]), 'órfão antigo coletado');
  for (const id of [E.ids['Alfa.md'], ...E.orphans.trashed]) assert.ok(hist.includes(id), 'histórico ativo mantido: ' + id);
  assert.deepEqual(JSON.parse(vault['.urbe/compositions.v2.json']).items[0].sources, [E.ids['Alfa.md'], ...E.orphans.trashed]);
  assert.equal(JSON.parse(vault['.urbe/vault.json']).maintenance.at(-1).kind, 'gc');
  assert.deepEqual(h.errors, [], 'sem erros: ' + h.errors.join(' | '));
  console.log('gc.e2e: ok');
} finally { await app.close(); }
