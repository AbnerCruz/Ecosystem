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
const NAMES = ['v1-mapa-v2', 'v1-mapa-v4', 'v1-orfaos', 'v1-notas-sem-id', 'v1-cidades-mescladas', 'v1-personalizacao', 'v1-paginas', 'v1-mundo-antigo'];

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
    // formato do vault (RM-F1-04/07): vault.json criado; arquivos .urbe v1 nunca reescritos; backup restaurável quando havia estado 1.x
    const vj = JSON.parse(vault['.urbe/vault.json'] || 'null');
    assert.equal(vj?.formatVersion, 2, `${name}: vault.json criado`);
    const v1 = ['.urbe/history.json', '.urbe/trash.json', '.urbe/compositions.json'].filter((r) => raw(name).has(r));
    for (const rel of v1) assert.equal(vault[rel], raw(name).get(rel), `${name}: ${rel} (v1) intacto`);
    if (raw(name).has('.urbe/mapa.json')) {
      const b = vj.migrations[0]?.backup; assert.ok(b, `${name}: migração registrada com backup`);
      assert.ok(vault[b + '/manifest.json'], `${name}: manifest do backup`);
      assert.equal(vault[b + '/files/urbe/mapa.json'], raw(name).get('.urbe/mapa.json'), `${name}: backup do mapa original`);
    }
    assert.deepEqual(h.errors, [], `${name}: sem erros: ` + h.errors.join(' | '));
    await h.ctx.close();
    console.log('  ok', name);
  }

  // proteção forward no app real (REQ-035): nada de versão maior é alterado
  for (const name of ['futuro-desconhecido', 'futuro-v2', 'vault-futuro']) {
    const expect = JSON.parse(readFileSync(join(DIR, name, 'expect.json'), 'utf8'));
    const h = await app.newPage({ seed: raw(name) });
    await openApp(h, { docs: 1 });
    await h.page.evaluate(() => { const d = UrbeCore.service('documents').list().find((x) => x.path === 'Alfa.md'); UrbeCore.service('documents').upsert({ ...d, content: d.content + '\nedição do teste\n' }); UrbeCore.commands.execute('workspace.save'); });
    await h.page.waitForTimeout(1500);
    const vault = await readVault(h.page);
    for (const [rel, hash] of Object.entries(expect.futureHashes)) assert.equal(sha(vault[rel] ?? ''), hash, `${name}: ${rel} de versão maior preservado`);
    if (expect.futureMapa) assert.equal(JSON.parse(vault['.urbe/mapa.json']).v, 99, `${name}: mapa de versão maior preservado pelo app`);
    if (expect.readOnly) {
      const original = raw(name);
      for (const [rel, c] of original) assert.equal(typeof vault[rel] === 'string' ? vault[rel] : null, typeof c === 'string' ? c : null, `${name}: ${rel} inalterado (somente leitura)`);
      assert.deepEqual(Object.keys(vault).filter((k) => !original.has(k)), [], `${name}: nenhum arquivo novo (Tutorial, backup, vault.json…)`);
    } else assert.match(vault['Alfa.md'] || '', /edição do teste/, `${name}: notas continuam gravando`);
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
