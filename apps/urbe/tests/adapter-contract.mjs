// Contrato do adaptador de persistência (REQ-028, RM-F1-10): o adaptador de pasta (FSA) sobre o disco real do Electron
// (ponte + native/desktop/vault-fs.js). IDB e OPFS rodam no navegador (tests/e2e/adapters.e2e.mjs).
import assert from 'node:assert/strict';
import fs from 'node:fs'; import os from 'node:os'; import path from 'node:path'; import vm from 'node:vm';
import { createRequire } from 'node:module';
import suite from './lib/adapter-contract-suite.mjs';

const require = createRequire(import.meta.url);
const read = (f) => fs.readFileSync(new URL('../' + f, import.meta.url), 'utf8');
const { createVaultFS } = require('../native/desktop/vault-fs.js');
const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'urbe-adapter-'));
const vfs = createVaultFS(() => tmp);

const doc = { documentElement: { classList: { add() {} } }, addEventListener() {}, getElementById() { return null; }, querySelectorAll() { return []; } };
const win = { UrbeNative: { shell: 'electron', platform: 'linux', vault: async () => ({ label: tmp, path: tmp }), fs: vfs }, navigator: {}, document: doc };
const c = { window: win, document: doc, console, File, Blob, TextEncoder, TextDecoder, DOMException, ArrayBuffer, Uint8Array, Symbol, Promise, fetch, setTimeout, clearTimeout };
vm.createContext(c);
for (const f of ['src/core/artifacts.js', 'src/native/bridge.js', 'src/persistence/adapters/fsa.js', 'src/persistence/adapters/idb.js', 'src/persistence/adapters/router.js']) vm.runInContext(read(f), c, { filename: f });

// FSA sobre a raiz da ponte nativa (um vault só, "Urbe")
const root = await win.UrbeNativeFS.root();
const adapter = win.UrbeAdapters.fsa.create({ root: () => root });
const results = await suite(adapter, { vault: 'Urbe', manageCities: false });
console.log(results.join('\n'));
assert.deepEqual(results.filter((r) => r.startsWith('FAIL')), [], 'contrato do adaptador FSA/Electron');
assert.ok(results.length >= 7);
// nada escapou da pasta do vault
assert.deepEqual(fs.readdirSync(path.dirname(tmp)).filter((n) => n.startsWith('urbe-adapter-') && path.join(path.dirname(tmp), n) !== tmp && n.endsWith('.md')), []);
assert.equal(win.UrbeAdapters.assertAdapter(adapter), adapter);
assert.throws(() => win.UrbeAdapters.assertAdapter({ list() {} }), /adaptador sem/);

// router: escolhe o adaptador ativo e expõe os nomes em português do legado
let modo = 'pasta';
const fakeIdb = { kind: 'idb', cities: async () => ['idb'], createCity() {}, removeCity() {}, list: async () => ['x'], read: async () => 'idb', readBlob() {}, write() {}, writeBlob() {}, remove() {}, createFolder() {}, removeFolder() {} };
const router = win.UrbeAdapters.router({ mode: () => modo, idb: fakeIdb, fsa: adapter });
assert.equal(await router.ler('Urbe', 'Pasta/Sub/b.md'), '# b', 'modo pasta usa FSA');
modo = 'interno';
assert.equal(await router.ler('Urbe', 'x'), 'idb'); assert.deepEqual(await router.listar('Urbe'), ['x']); assert.deepEqual(await router.cidades(), ['idb']);
console.log('adapter-contract: ok');
