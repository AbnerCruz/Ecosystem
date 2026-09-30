// E2E: a mesma suíte de contrato roda contra o adaptador IDB e o adaptador FSA (OPFS = File System Access real do Chromium).
import assert from 'node:assert/strict';
import { launchApp, openApp } from '../../tools/lib/browser.mjs';
import { suiteSource } from '../lib/adapter-contract-suite.mjs';

const app = await launchApp();
try {
  const h = await app.newPage();
  await openApp(h, { docs: 40 });
  const run = (kind) => h.page.evaluate(async ({ src, kind }) => {
    const suite = new Function('return (' + src + ')')();
    let adapter;
    if (kind === 'idb') adapter = UrbeAdapters.idb.create({ dbName: 'contrato-idb', mime: () => '' });
    else { const root = await navigator.storage.getDirectory(); adapter = UrbeAdapters.fsa.create({ root: () => root }); }
    UrbeAdapters.assertAdapter(adapter);
    return await suite(adapter, { vault: 'suite-c1-' + kind });
  }, { src: suiteSource, kind });
  for (const kind of ['idb', 'fsa']) {
    const res = await run(kind);
    console.log(`  [${kind}]\n` + res.map((r) => '    ' + r).join('\n'));
    assert.deepEqual(res.filter((r) => r.startsWith('FAIL')), [], `contrato do adaptador ${kind}`);
    assert.ok(res.length >= 8);
  }
  assert.deepEqual(h.errors, [], 'sem erros: ' + h.errors.join(' | '));
  console.log('adapters.e2e: ok');
} finally { await app.close(); }
