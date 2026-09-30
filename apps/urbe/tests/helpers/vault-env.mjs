// Ambiente Node (vm) com o núcleo + persistência e um adaptador de vault em memória, para testes de dados (REQ-037).
import fs from 'node:fs';
import vm from 'node:vm';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.join(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
export const FIXTURES_DIR = path.join(ROOT, 'tests/fixtures/vaults');
const MODULES = ['src/core/artifacts.js','src/core/core.js', 'src/core/documents.js', 'src/core/trash.js', 'src/core/history.js', 'src/composition/store.js', 'src/persistence/vault-meta.js','src/persistence/backup.js','src/persistence/identity.js','src/persistence/workspace.js', 'src/persistence/gc.js', 'src/world/projection.js'];

/** Lê uma fixture do disco → Map(path relativo → string). */
export function readFixture(name) {
  const base = path.join(FIXTURES_DIR, name), out = new Map();
  (function walk(d) { for (const e of fs.readdirSync(d, { withFileTypes: true })) { const f = path.join(d, e.name); if (e.isDirectory()) walk(f); else if (e.name !== 'expect.json') out.set(path.relative(base, f).split(path.sep).join('/'), fs.readFileSync(f, 'utf8')); } })(base);
  return out;
}
export const readExpect = (name) => JSON.parse(fs.readFileSync(path.join(FIXTURES_DIR, name, 'expect.json'), 'utf8'));

/** files: Map(path → string). Retorna o ambiente pronto para p.load('V'). */
export function createEnv(files) {
  const store = new Map([...files].map(([p, c]) => ['V/' + p, c]));
  const adapter = {
    async list(v) { return [...store.keys()].filter((k) => k.startsWith(v + '/')).map((k) => k.slice(v.length + 1)); },
    async read(v, p) { return store.has(v + '/' + p) ? store.get(v + '/' + p) : null; },
    async write(v, p, c) { store.set(v + '/' + p, c); },
    async remove(v, p) { store.delete(v + '/' + p); },
  };
  const context = { window: { crypto: globalThis.crypto }, setTimeout, clearTimeout, console, TextEncoder };
  vm.createContext(context);
  for (const m of MODULES) vm.runInContext(fs.readFileSync(path.join(ROOT, m), 'utf8'), context, { filename: m });
  const core = context.window.UrbeCore;
  const p = core.service('persistence');
  p.configure(adapter);
  return {
    core, p, adapter, context,
    docs: core.service('documents'), trash: core.service('trash'), history: core.service('history'), compositions: core.service('compositions'), world: core.service('world.projection'),
    disk: () => new Map([...store].map(([k, v]) => [k.slice(2), v])),
    has: (rel) => store.has('V/' + rel), get: (rel) => store.get('V/' + rel),
  };
}
