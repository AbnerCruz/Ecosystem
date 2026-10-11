// UC-19 oracle for Personalização: the UNMODIFIED 1.8.4-beta src/customize/customize.js run by
// Node with a minimal page around it. For each case it records normalize(text) (config, errors,
// warnings) and, starting from that configuration, the tema.json text that each set(patch)
// writes (gravar → docs.upsert). Test-only: JavaScript never runs inside the native app.
//   node customize-oracle.mjs <apps/urbe/src> < cases.json
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';

const src = process.argv[2];
const cases = JSON.parse(readFileSync(0, 'utf8'));
const out = [];
for (const c of cases) {
  const files = new Map(Object.entries(c.files || {}));
  const writes = [];
  const handlers = {};
  const el = () => ({ style: { setProperty() { }, removeProperty() { } }, setAttribute() { }, remove() { }, textContent: '' });
  const services = {
    documents: {
      get: p => files.has(p) ? { id: p, path: p, title: p.split('/').pop(), content: files.get(p), revision: 1 } : null,
      list: () => [...files.keys()].map(p => ({ id: p, path: p, title: p.split('/').pop(), content: files.get(p) })),
      upsert: d => { files.set(d.path, d.content); writes.push(d.content); return d; }
    },
    persistence: { vault: 'Urbe' }
  };
  const ctx = {
    Math, JSON, Object, Array, String, Number, Map, Set, Promise, isFinite, console, setTimeout() { }, clearTimeout() { },
    document: { documentElement: Object.assign(el(), { style: { setProperty() { }, removeProperty() { }, colorScheme: '' } }), querySelector: () => null, getElementById: () => null, createElement: el, head: { appendChild() { } } },
    location: { search: '' }, localStorage: { getItem: () => null, setItem() { }, removeItem() { } },
    UrbeCore: {
      service: n => services[n] || null, provide: (n, api) => { services[n] = api; },
      events: { on: (n, f) => { (handlers[n] = handlers[n] || []).push(f); }, emit() { } },
      commands: { register() { } }
    }
  };
  ctx.window = ctx; ctx.globalThis = ctx;
  runInNewContext(readFileSync(`${src}/customize/customize.js`, 'utf8'), ctx, { filename: 'customize.js' });
  const C = services.customize;
  const n = C.normalize(c.text);
  const r = { config: JSON.stringify(n.config), errors: n.errors, warnings: n.warnings, writes: [] };
  if (!n.errors.length || c.sets) {
    C.replace(n.config);
    r.writes.push({ file: writes[writes.length - 1] ?? null, config: JSON.stringify(C.get()) });
    for (const patch of c.sets || []) {
      const before = writes.length, res = C.set(patch);
      r.writes.push({ file: writes.length > before ? writes[writes.length - 1] : null, config: JSON.stringify(C.get()), errors: res.errors.length });
    }
  }
  out.push(r);
}
process.stdout.write(JSON.stringify(out));
