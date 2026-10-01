// E2E: exportar o vault em ZIP e importá-lo em um app novo (REQ-061; base de REQ-044 e REQ-058).
import assert from 'node:assert/strict';
import { mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { launchApp, openApp, waitSaved } from '../../tools/lib/browser.mjs';
import { makeVault } from '../../tools/perf/make-vault.mjs';

const app = await launchApp();
try {
  const seed = makeVault('S');
  const a = await app.newPage({ seed });
  await openApp(a, { docs: seed.notes });
  await a.page.evaluate(() => { localStorage.setItem('urbe.tip.teste', '1'); localStorage.setItem('urbe.ai.apiKey', 'sk-SEGREDO-E2E'); return UrbeAIStore.setConfig({ provider: 'openai', apiKey: 'sk-SEGREDO-E2E' }); });
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

  // manifesto (RM-F1-18, REQ-044): todo arquivo com hash; estado local sem segredos
  const man = await a.page.evaluate(async (bytes) => { const z = await JSZip.loadAsync(new Uint8Array(bytes)), m = UrbeExportManifest.parse(await z.file('urbe-export.json').async('string')), all = new Map();
    for (const n of Object.keys(z.files)) if (!z.files[n].dir && n !== 'urbe-export.json') all.set(n, await z.file(n).async('uint8array'));
    return { m: m.manifest, check: await UrbeExportManifest.verify(m.manifest, all) }; }, [...readFileSync(file)]);
  assert.equal(man.check.ok, true, 'hashes do export íntegros: ' + JSON.stringify(man.check));
  assert.equal(man.m.files.length, names.length - 1, 'manifesto cobre todos os arquivos');
  assert.ok(man.m.state.localStorage['urbe.tip.teste'], 'preferência permitida exportada');
  const zipText = readFileSync(file).toString('latin1');
  assert.ok(!zipText.includes('sk-SEGREDO-E2E'), 'chave de IA nunca vai para o ZIP (nem comprimida no manifesto)');
  assert.doesNotMatch(JSON.stringify(man.m.state), /SEGREDO|apiKey/);
  assert.deepEqual(a.errors, [], 'sem erros: ' + a.errors.join(' | '));

  // importar o ZIP pelo Explorer num app novo (bug 1.8.2: não descompactava) e conferir por hash
  const importar = async (h, path) => {
    await h.page.evaluate(() => { delete window.showDirectoryPicker; delete window.showOpenFilePicker; });
    const [chooser] = await Promise.all([h.page.waitForEvent('filechooser'), h.page.evaluate(() => { UrbeCore.service('legacy.runtime').importFiles(null); })]);
    await chooser.setFiles(path);
  };
  const b = await app.newPage();
  await openApp(b, { docs: 1 });
  await importar(b, file);
  await b.page.waitForSelector('.udlg [data-primary]'); // "Preferências do export": aplicar
  assert.match(await b.page.textContent('.udlg-msg'), /preferências/);
  await b.page.click('.udlg [data-primary]');
  await b.page.waitForFunction((n) => UrbeCore.service('documents').list().filter((d) => d.path.endsWith('.md') && !d.path.startsWith('Tutorial/')).length >= n, seed.notes, { timeout: 30000 });
  await waitSaved(b.page);
  const imported = await b.page.evaluate(() => UrbeCore.service('documents').list().map((d) => [d.path, d.content]));
  assert.ok(!imported.some(([p]) => p.includes('.urbe/') || p === 'urbe-export.json'), 'nem .urbe/ nem o manifesto viram notas');
  const byName = new Map(imported.map(([p, c]) => [p.split('/').pop(), c]));
  const zipped = await b.page.evaluate(async (bytes) => { const z = await JSZip.loadAsync(new Uint8Array(bytes)), out = {}; for (const n of Object.keys(z.files)) if (n.endsWith('.md') && !n.startsWith('.')) out[n.split('/').pop()] = await z.file(n).async('string'); return out; }, [...readFileSync(file)]);
  for (const [n, c] of Object.entries(zipped)) assert.equal(byName.get(n), c, 'conteúdo importado = exportado: ' + n);
  assert.equal(await b.page.evaluate(() => localStorage.getItem('urbe.tip.teste')), '1', 'preferência aplicada');
  assert.equal(await b.page.evaluate(() => localStorage.getItem('urbe.ai.apiKey')), null);
  assert.deepEqual(b.errors, [], 'sem erros: ' + b.errors.join(' | '));

  // ZIP adulterado: avisa e deixa cancelar
  const tampered = file.replace(/\.zip$/, '-adulterado.zip');
  const bytes = await a.page.evaluate(async (bytes) => { const z = await JSZip.loadAsync(new Uint8Array(bytes)); const n = Object.keys(z.files).find((x) => x.endsWith('.md') && !x.startsWith('.')); z.file(n, 'ADULTERADO'); return [...await z.generateAsync({ type: 'uint8array' })]; }, [...readFileSync(file)]);
  writeFileSync(tampered, Buffer.from(bytes));
  const c = await app.newPage();
  await openApp(c, { docs: 1 });
  const before = await c.page.evaluate(() => UrbeCore.service('documents').list().length);
  await importar(c, tampered);
  await c.page.waitForSelector('.udlg [data-primary]');
  assert.match(await c.page.textContent('.udlg-msg'), /não batem com o manifesto/);
  await c.page.click('.udlg [data-cancel].ui-btn');
  await c.page.waitForTimeout(500);
  assert.equal(await c.page.evaluate(() => UrbeCore.service('documents').list().length), before, 'cancelar não importa nada');
  // manifesto de versão mais nova: recusa (política DATA-CATALOG §9) sem importar nada
  const future = file.replace(/\.zip$/, '-futuro.zip');
  writeFileSync(future, Buffer.from(await a.page.evaluate(async () => { const z = new JSZip(); z.file('Nota futura.md', '# x\n'); z.file('urbe-export.json', JSON.stringify({ format: 'urbe-export', formatVersion: 9, files: [] })); return [...await z.generateAsync({ type: 'uint8array' })]; })));
  await importar(c, future);
  await c.page.waitForSelector('.udlg [data-primary]');
  assert.match(await c.page.textContent('.udlg-msg'), /versão mais nova/);
  await c.page.click('.udlg [data-primary]'); await c.page.waitForTimeout(300);
  assert.equal(await c.page.evaluate(() => UrbeCore.service('documents').list().some((d) => d.path.endsWith('Nota futura.md'))), false, 'nada importado');
  assert.deepEqual(c.errors, [], 'sem erros: ' + c.errors.join(' | '));
  console.log('zip.e2e: ok');
} finally { await app.close(); }
