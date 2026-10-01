// E2E: o app abre em Chromium real com um vault fixture, sem erros de console, e o Tutorial é criado na primeira abertura (RM-F4-04).
import assert from 'node:assert/strict';
import { launchApp, openApp } from '../../tools/lib/browser.mjs';
import { makeVault } from '../../tools/perf/make-vault.mjs';

const app = await launchApp();
try {
  // 1) vault semeado (S): abre com os documentos, sem erros
  const seed = makeVault('S');
  const a = await app.newPage({ seed });
  await openApp(a, { docs: seed.notes });
  const info = await a.page.evaluate(() => ({ title: document.title, docs: UrbeCore.service('documents').list().length, version: UrbeCore.version }));
  assert.equal(info.docs >= seed.notes, true, 'documentos carregados: ' + info.docs);
  assert.match(info.title, /^Urbe v/); assert.equal(info.title, 'Urbe v' + info.version);
  assert.deepEqual(a.errors, [], 'sem erros de console/página: ' + a.errors.join(' | '));

  // 2) primeira abertura sem vault: cria o Tutorial
  const b = await app.newPage();
  await openApp(b, { docs: 40 });
  const tut = await b.page.evaluate(() => UrbeCore.service('documents').list().filter((d) => d.path.startsWith('Tutorial/')).length);
  assert.ok(tut >= 40, 'Tutorial criado: ' + tut);
  assert.deepEqual(b.errors, [], 'sem erros: ' + b.errors.join(' | '));
  console.log('smoke.e2e: ok');
} finally { await app.close(); }
