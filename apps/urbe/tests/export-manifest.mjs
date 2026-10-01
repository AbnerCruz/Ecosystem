// Manifesto do export (REQ-044, RM-F1-18): hashes íntegros, adulteração detectada, estado local só com chaves permitidas
// e NUNCA chaves de IA/segredos.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';

const ctx = { window: { crypto: globalThis.crypto } }; vm.createContext(ctx);
vm.runInContext(fs.readFileSync(new URL('../src/persistence/export-manifest.js', import.meta.url), 'utf8'), ctx);
const M = ctx.window.UrbeExportManifest;
const enc = (s) => new TextEncoder().encode(s);
const J = (x) => JSON.parse(JSON.stringify(x));
class Storage { constructor(o) { this.m = new Map(Object.entries(o)); } get length() { return this.m.size; } key(i) { return [...this.m.keys()][i]; } getItem(k) { return this.m.has(k) ? this.m.get(k) : null; } setItem(k, v) { this.m.set(k, String(v)); } }

// round-trip: build → parse → verify ok
const files = [['Alfa.md', enc('# Alfa\n')], ['.urbe/mapa.json', enc('{"v":4}')], ['Anexos/x.bin', new Uint8Array([0, 255, 3])]];
const man = J(await M.build(files.map(([path, bytes]) => ({ path, bytes })), { appVersion: '2.0.0', vault: 'Urbe', vaultFormat: 2, state: { localStorage: {} } }));
assert.equal(man.format, 'urbe-export'); assert.equal(man.formatVersion, 1); assert.equal(man.vault.formatVersion, 2);
assert.deepEqual(man.files.map((f) => f.path), ['.urbe/mapa.json', 'Alfa.md', 'Anexos/x.bin']);
assert.equal(man.files.find((f) => f.path === 'Anexos/x.bin').size, 3);
const p = M.parse(JSON.stringify(man)); assert.equal(p.state, 'current');
const entries = () => new Map(files.map(([k, v]) => [k, v]));
assert.equal((await M.verify(p.manifest, entries())).ok, true);

// adulteração: conteúdo alterado (mesmo tamanho), arquivo faltando, arquivo a mais
const t = entries(); t.set('Alfa.md', enc('# Alfx\n')); t.delete('Anexos/x.bin'); t.set('intruso.md', enc('x'));
const v = J(await M.verify(p.manifest, t));
assert.deepEqual([v.ok, v.mismatched, v.missing, v.extra], [false, ['Alfa.md'], ['Anexos/x.bin'], ['intruso.md']]);
assert.equal(M.parse('{').state, 'corrupt'); assert.equal(M.parse('{"format":"outro","formatVersion":1,"files":[]}').state, 'corrupt');
assert.equal(M.parse('{"format":"urbe-export","formatVersion":9,"files":[]}').state, 'future');

// estado local: lista permitida; aprovações de plugins só deste vault; segredos nunca
const ls = new Storage({
  'urbe.explorer.v2': '{"favorites":["a"]}', 'urbe.editor.workspace.v1': '{"tabs":[]}', 'urbe.tip.cidade': '1',
  'urbe.plugins.v1': JSON.stringify({ 'Urbe::Personalização/plugins/ola.js': 'sha256:aa', 'Outro::p.js': 'sha256:bb' }),
  'urbe.ai.config.v1': '{"apiKey":"sk-SEGREDO"}', 'urbe.ai.global.v21': 'x', 'openai-api-key': 'sk-2', 'urbe.plugin.Urbe.ola.token': 't', 'urbe.tip.apiKey': 'k',
  'urbe.modoSeguro': '1', 'urbe-page-theme': 'dark',
});
const st = J(M.collectState(ls, 'Urbe'));
assert.deepEqual(Object.keys(st.localStorage).sort(), ['urbe.editor.workspace.v1', 'urbe.explorer.v2', 'urbe.tip.cidade']);
assert.deepEqual(st.plugins, { 'Personalização/plugins/ola.js': 'sha256:aa' });
assert.doesNotMatch(JSON.stringify(st), /SEGREDO|sk-|token|apiKey/i, 'nenhum segredo no estado exportado');
for (const k of ['urbe.ai.config.v1', 'openai-api-key', 'urbe.plugin.Urbe.ola.token', 'urbe.tip.apiKey']) assert.equal(M.allowed(k), false, k);

// aplicar noutro aparelho/vault: aprovações remapeadas; chaves proibidas recusadas mesmo se vierem no ZIP
const dest = new Storage({ 'urbe.plugins.v1': JSON.stringify({ 'Casa::x.js': 'sha256:cc' }) });
const wrote = J(M.applyState(dest, { ...st, localStorage: { ...st.localStorage, 'urbe.ai.config.v1': '{"apiKey":"sk-X"}', 'urbe.qualquer': 'y' } }, 'Casa'));
assert.deepEqual(wrote.sort(), ['urbe.editor.workspace.v1', 'urbe.explorer.v2', 'urbe.plugins.v1', 'urbe.tip.cidade']);
assert.equal(dest.getItem('urbe.ai.config.v1'), null); assert.equal(dest.getItem('urbe.qualquer'), null);
assert.deepEqual(JSON.parse(dest.getItem('urbe.plugins.v1')), { 'Casa::x.js': 'sha256:cc', 'Casa::Personalização/plugins/ola.js': 'sha256:aa' });
console.log('export-manifest: ok');
