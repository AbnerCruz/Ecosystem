// Proteção de layout (REQ-043, RM-F1-17): foto antes de reorganizar, desfazer por ID, backup do mapa e registro;
// sem backup (e sem erro) quando o vault ou o mapa são somente leitura.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';

const ctx = { window: { crypto: globalThis.crypto }, TextEncoder, console }; vm.createContext(ctx);
for (const f of ['src/persistence/backup.js', 'src/world/layout-guard.js']) vm.runInContext(fs.readFileSync(new URL('../' + f, import.meta.url), 'utf8'), ctx);
const G = ctx.window.UrbeLayoutGuard;
const disk = new Map([['V/.urbe/mapa.json', '{"v":4,"mundo":"placas-0"}']]);
const adapter = { async list(v) { return [...disk.keys()].filter((k) => k.startsWith(v + '/')).map((k) => k.slice(v.length + 1)); }, async read(v, p) { return disk.get(v + '/' + p) ?? null; }, async write(v, p, c) { disk.set(v + '/' + p, c); }, async remove(v, p) { disk.delete(v + '/' + p); } };
const log = [];
const P = { adapter, vault: 'V', readOnly: false, mapaReadonly: false, snapshot: new Map([['.urbe/mapa.json', disk.get('V/.urbe/mapa.json')]]), metadataProvider: () => null, recordMaintenance: async (e) => { log.push(e); return true; } };
const world = { regions: [{ id: 'r1', x: 1, y: 1, w: 5, h: 5, cells: ['1,1'], color: '#fff', parentId: null }], buildings: [{ id: 'b1', x: 2, y: 2, regionId: 'r1' }, { id: 'b2', x: 3, y: 3, regionId: null }] };
let restored = 0;
const g = G.create({ world, persistence: () => P, onRestored: () => restored++ });

assert.equal(g.undo(), false, 'nada a desfazer');
const last = g.before({ reason: 'mundo', from: 'placas-0', to: 'placas-1' });
// reorganização muta tudo
world.regions[0].x = 90; world.regions[0].cells = ['90,90']; world.buildings[0].x = 91; world.buildings[1].regionId = 'r1';
const dir = await last.backup;
assert.match(dir, /^\.urbe\/backup\/\d{8}-\d{6}-layout-layout$/);
assert.equal(disk.get('V/' + dir + '/files/urbe/mapa.json'), '{"v":4,"mundo":"placas-0"}', 'backup = mapa antes de reorganizar');
assert.equal(JSON.parse(disk.get('V/' + dir + '/manifest.json')).reason, 'mundo');
assert.deepEqual(JSON.parse(JSON.stringify(log)), [{ kind: 'layout', reason: 'mundo', from: 'placas-0', to: 'placas-1', backup: dir }]);
assert.equal(g.undo(), true); assert.equal(restored, 1);
assert.deepEqual([world.regions[0].x, [...world.regions[0].cells], world.buildings[0].x, world.buildings[1].regionId], [1, ['1,1'], 2, null], 'desfazer restaura regiões e casas');
assert.equal(g.last(), null, 'desfazer uma vez só');

// mapa atual vem do mundo quando ele já carregou
P.metadataProvider = () => ({ v: 4, mundo: 'placas-1', agora: true });
const d2 = await g.before({ reason: 'reorganizar' }).backup;
assert.match(disk.get('V/' + d2 + '/files/urbe/mapa.json'), /"agora": true/);

// somente leitura: reorganiza em memória, mas não grava backup nem registro (e não quebra)
for (const flag of ['readOnly', 'mapaReadonly']) {
  const before = disk.size, n = log.length; P[flag] = true;
  assert.equal(await g.before({ reason: 'x' }).backup, null); assert.equal(disk.size, before); assert.equal(log.length, n);
  P[flag] = false;
}
console.log('layout-guard: ok');
