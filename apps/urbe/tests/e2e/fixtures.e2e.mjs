// E2E: cada vault histórico abre no app real sem erro e sem alterar nenhuma nota (REQ-037, REQ-007); o formato IDB legado migra.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { launchApp, openApp, readVault, waitSaved, seedKv } from '../../tools/lib/browser.mjs';

const DIR = new URL('../fixtures/vaults/', import.meta.url).pathname;
const sha = (s) => createHash('sha256').update(s).digest('hex');
const TEXT = /\.(md|markdown|txt|html?|js|mjs|css|json|ya?ml|csv)$/i;
function raw(name) {
  const out = new Map();
  (function walk(d, pre) { for (const e of readdirSync(d, { withFileTypes: true })) { if (e.isDirectory()) walk(join(d, e.name), pre + e.name + '/'); else if (e.name !== 'expect.json') { const rel = pre + e.name, buf = readFileSync(join(d, e.name)); out.set(rel, TEXT.test(rel) ? buf.toString('utf8') : new Uint8Array(buf)); } } })(join(DIR, name), '');
  return out;
}
const NAMES = ['v1-mapa-v2', 'v1-mapa-v4', 'v1-notas-sem-id', 'v1-cidades-mescladas', 'v1-personalizacao', 'v1-paginas', 'v1-mundo-antigo'];

const app = await launchApp();
try {
  for (const name of NAMES) {
    const expect = JSON.parse(readFileSync(join(DIR, name, 'expect.json'), 'utf8'));
    const h = await app.newPage({ seed: raw(name) });
    await openApp(h, { docs: expect.notes.length });
    await h.page.evaluate(() => UrbeCore.commands.execute('workspace.save'));
    await waitSaved(h.page);
    const vault = await readVault(h.page);
    const docs = await h.page.evaluate(() => Object.fromEntries(UrbeCore.service('documents').list().map((d) => [d.path, d.content])));
    for (const rel of expect.notes) assert.ok(rel in docs, `${name}: documento ${rel} carregado`);
    for (const [rel, hash] of Object.entries(expect.noteHashes)) if (TEXT.test(rel) && !rel.startsWith('.urbe/')) assert.equal(sha(typeof vault[rel] === 'string' ? vault[rel] : ''), hash, `${name}: bytes de ${rel} no vault após salvar`);
    for (const [rel, hash] of Object.entries(expect.keepHashes || {})) assert.equal(sha(vault[rel] || ''), hash, `${name}: ${rel} preservado`);
    assert.deepEqual(h.errors, [], `${name}: sem erros: ` + h.errors.join(' | '));
    await h.ctx.close();
    console.log('  ok', name);
  }

  // formato v1 que só existia no IndexedDB (kv["cidade"]): migrarAntiga
  const legado = JSON.parse(readFileSync(join(DIR, 'idb-legado-kv-cidade.json'), 'utf8'));
  const g = await app.newPage();
  await g.page.goto(g.url + '/manifest.webmanifest');
  await seedKv(g.page, [['cidade', legado]]);
  await openApp(g, { docs: 1 });
  const alfa = await g.page.evaluate(() => { const d = UrbeCore.service('documents').list().find((x) => /Alfa\.md$/.test(x.path)); return d && d.content; });
  assert.match(alfa || '', /Primeira nota com \[\[Beta\]\]/, 'IDB legado migrou a nota Alfa');
  assert.deepEqual(g.errors, [], 'sem erros: ' + g.errors.join(' | '));
  console.log('  ok idb-legado');
  console.log('fixtures.e2e: ok');
} finally { await app.close(); }
