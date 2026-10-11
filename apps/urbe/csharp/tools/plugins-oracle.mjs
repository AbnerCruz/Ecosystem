// UC-19 oracle for the data half of Personalização → Plugins: the UNMODIFIED 1.8.4-beta
// src/customize/plugins.js run by Node. For a vault file set it records list() (name, version,
// description read without running the code), the fingerprint of each file and what
// create(name, template) writes. Plugins themselves are never run. Test-only.
//   node plugins-oracle.mjs <apps/urbe/src> < {files, creates:[{name,template}]}
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';

const src = process.argv[2];
const input = JSON.parse(readFileSync(0, 'utf8'));
const files = new Map(Object.entries(input.files));
const el = () => ({ style: {}, setAttribute() { }, remove() { }, textContent: '' });
const services = {
  documents: {
    get: p => files.has(p) ? { id: p, path: p, title: p.split('/').pop(), content: files.get(p), tags: [], links: [] } : null,
    list: () => [...files.keys()].map(p => ({ id: p, path: p, title: p.split('/').pop(), content: files.get(p) })),
    upsert: d => { files.set(d.path, d.content); return { ...d, id: d.path, title: d.path.split('/').pop(), tags: [], links: [] }; }
  },
  persistence: { vault: 'Urbe' }
};
const ctx = {
  Math, JSON, Object, Array, String, Number, Map, Set, Promise, console, setTimeout() { }, clearTimeout() { }, Uint8Array, TextEncoder,
  crypto: globalThis.crypto,
  document: { getElementById: () => null, createElement: el, head: { appendChild() { } } },
  localStorage: { getItem: () => null, setItem() { } },
  UrbeCore: { service: n => services[n] || null, provide: (n, a) => { services[n] = a; }, events: { on() { }, emit() { } }, commands: { register() { } } }
};
ctx.window = ctx;
runInNewContext(readFileSync(`${src}/customize/plugins.js`, 'utf8'), ctx, { filename: 'plugins.js' });
const P = services.plugins;
const out = { templates: P.TEMPLATES, list: P.list().map(x => ({ path: x.path, nome: x.nome, versao: x.versao, descricao: x.descricao, tamanho: x.tamanho })), fingerprints: {}, creates: [] };
for (const [p, c] of files) out.fingerprints[p] = await P.fingerprint(c);
for (const c of input.creates) { const d = P.create(c.name, c.template); out.creates.push({ path: d.path, content: files.get(d.path) }); }
process.stdout.write(JSON.stringify(out));
