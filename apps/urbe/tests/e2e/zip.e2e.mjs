// E2E: exportar o vault em ZIP e importá-lo em um app novo (REQ-061; base de REQ-044 e REQ-058).
import assert from 'node:assert/strict';
import { mkdtempSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { launchApp, openApp, waitSaved } from '../../tools/lib/browser.mjs';
import { makeVault } from '../../tools/perf/make-vault.mjs';

const app = await launchApp();
try {
  const seed = makeVault('S');
  const a = await app.newPage({ seed });
  await openApp(a, { docs: seed.notes });
  await waitSaved(a.page);
  await a.page.evaluate(() => UrbeCore.commands.execute('workspace.save'));

  // exportar
  const [download] = await Promise.all([a.page.waitForEvent('download', { timeout: 30000 }), a.page.evaluate(() => UrbeCore.service('legacy.runtime').exportZip())]);
  const file = join(mkdtempSync(join(tmpdir(), 'urbe-zip-')), download.suggestedFilename());
  await download.saveAs(file);
  const names = await a.page.evaluate(async (bytes) => Object.keys((await JSZip.loadAsync(new Uint8Array(bytes))).files).filter((n) => !n.endsWith('/')), [...readFileSync(file)]);
  assert.match(download.suggestedFilename(), /\.zip$/);
  assert.ok(names.filter((n) => n.endsWith('.md')).length >= seed.notes, 'todas as notas no ZIP: ' + names.length);
  assert.ok(names.includes('.urbe/mapa.json'), 'mapa no ZIP');
  assert.ok(!names.includes('.urbe/journal.json'), 'journal fora do ZIP');
  assert.ok(names.some((n) => n.startsWith('Anexos/') && n.endsWith('.png')), 'assets no ZIP');

  // A importação do ZIP (round-trip) entra com RM-F1-18: na 1.8.2 o Explorer trata o .zip como arquivo (não descompacta).
  assert.deepEqual(a.errors, [], 'sem erros: ' + a.errors.join(' | '));
  console.log('zip.e2e: ok');
} finally { await app.close(); }
