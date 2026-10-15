// UC-19 oracle: the reconciliation at the opening of the UNMODIFIED Urbe 1.8.4-beta (src/persistence/identity.js
// reconcile/pair/moveMeta, REQ-042): notes renamed or moved outside the app while it was closed keep id, house,
// region and assets. Each case gives the notes read from the disk, the map and the sidecar; the oracle records the
// ids, the renames, the ambiguous groups and the map after it. Test-only tool: never part of the native app.
//
//   node csharp/tools/legacy-identity-oracle.mjs > csharp/tests/fixtures/identity/reconcile-run-a.json
import { readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const ctx = { console, JSON, Object, Array, String, Number, Math, Map, Set, RegExp, Error };
ctx.window = ctx; ctx.globalThis = ctx;
vm.createContext(ctx);
vm.runInContext(readFileSync(join(root, 'src/persistence/identity.js'), 'utf8'), ctx, { filename: 'identity.js' });
const I = ctx.UrbeIdentity;
const fp = t => I.fingerprint(t);
const side = docs => ({ version: 1, docs: Object.fromEntries(Object.entries(docs).map(([id, [path, text]]) => [id, { path, fingerprint: fp(text), seen: '2026-01-01T00:00:00.000Z' }])) });

const cases = [
  { name: 'simple-rename',
    items: [['Bairro/Novo nome.md', '# Texto\n\nconteúdo'], ['Outra.md', '# Outra']],
    meta: { v: 4, notas: { 'Bairro/Velho.md': { id: 'n1', x: 10, y: 12, criado: 1 }, 'Outra.md': { id: 'n2', x: 3, y: 4 } }, regioes: [{ id: 'reg_a', caminho: 'Bairro', nome: 'Bairro', x: 0, y: 0, w: 20, h: 20 }] },
    identity: side({ n1: ['Bairro/Velho.md', '# Texto\n\nconteúdo  \n\n'], n2: ['Outra.md', '# Outra'] }) },
  { name: 'moved-to-another-folder',
    items: [['B/Nota.md', 'mesmo texto'], ['A/Fica.md', 'x']],
    meta: { notas: { 'A/Nota.md': { id: 'n1', x: 5, y: 6, tags: ['t'] }, 'A/Fica.md': { id: 'n3', x: 1, y: 1 } }, regioes: [{ id: 'reg_a', caminho: 'A', nome: 'A' }, { id: 'reg_b', caminho: 'B', nome: 'B' }] },
    identity: side({ n1: ['A/Nota.md', 'mesmo texto'], n3: ['A/Fica.md', 'x'] }) },
  { name: 'folder-renamed',
    items: [['Novo/Sub/Um.md', 'um'], ['Novo/Dois.md', 'dois']],
    meta: { notas: { 'Velho/Sub/Um.md': { id: 'n1', x: 7, y: 8, anexos: [{ relPath: 'Velho/Sub/img.png', folderPath: 'Velho/Sub' }] }, 'Velho/Dois.md': { id: 'n2', x: 9, y: 9 } },
      regioes: [{ id: 'reg_v', caminho: 'Velho', nome: 'Velho', x: 1, y: 1, w: 30, h: 30 }, { id: 'reg_s', caminho: 'Velho/Sub', nome: 'Sub' }],
      construcoes: [{ id: 'c1', caminho: 'Velho/Sub', files: [{ relPath: 'Velho/Sub/a.pdf', folderPath: 'Velho/Sub' }] }] },
    identity: side({ n1: ['Velho/Sub/Um.md', 'um'], n2: ['Velho/Dois.md', 'dois'] }),
    onDisk: ['Novo/Sub/Um.md', 'Novo/Dois.md', 'Novo/Sub/img.png', 'Novo/Sub/a.pdf', '.urbe/mapa.json'] },
  { name: 'ambiguous-copies',
    items: [['X/Copia 1.md', 'igual'], ['X/Copia 2.md', 'igual'], ['Vazia nova.md', '']],
    meta: { notas: { 'Original.md': { id: 'n1', x: 1, y: 2 }, 'Vazia.md': { id: 'n9', x: 0, y: 0 } } },
    identity: side({ n1: ['Original.md', 'igual'], n9: ['Vazia.md', ''] }) },
  { name: 'id-from-sidecar-same-path',
    items: [['Sem mapa.md', 'a'], ['Outra.md', 'b']],
    meta: null,
    identity: side({ s1: ['Sem mapa.md', 'a'], s2: ['Outra.md', 'zzz'] }) },
  { name: 'target-already-in-map',
    items: [['Novo.md', 'texto']],
    meta: { notas: { 'Velho.md': { id: 'n1', x: 1, y: 1 }, 'Novo.md': { x: 2, y: 2 } } },
    identity: side({ n1: ['Velho.md', 'texto'] }) },
  { name: 'old-folder-still-exists',
    items: [['Novo/Um.md', 'um'], ['Velho/Outro.md', 'outro']],
    meta: { notas: { 'Velho/Um.md': { id: 'n1', x: 4, y: 4 }, 'Velho/Outro.md': { id: 'n2', x: 5, y: 5 } }, regioes: [{ id: 'reg_v', caminho: 'Velho', nome: 'Velho' }] },
    identity: side({ n1: ['Velho/Um.md', 'um'], n2: ['Velho/Outro.md', 'outro'] }) },
  { name: 'no-identity',
    items: [['A.md', 'a']],
    meta: { notas: { 'B.md': { id: 'n1' } } },
    identity: null }
];

const out = cases.map(c => {
  const meta = c.meta ? JSON.parse(JSON.stringify(c.meta)) : null;
  const notes = (meta && meta.notas) || {};
  const items = c.items.map(([path, content]) => ({ id: (notes[path] && notes[path].id) || null, path, content }));
  const r = I.reconcile(items, meta, c.identity, c.onDisk ? new Set(c.onDisk) : undefined);
  return { name: c.name, items: c.items, metaBefore: c.meta, identity: c.identity, onDisk: c.onDisk || null,
    ids: items.map(i => i.id), renames: r.renames, ambiguous: r.ambiguous, metaAfter: meta,
    fingerprints: c.items.map(([, t]) => fp(t)) };
});
process.stdout.write(JSON.stringify({ source: 'apps/urbe/src/persistence/identity.js (1.8.4-beta)', cases: out }, null, 1) + '\n');
