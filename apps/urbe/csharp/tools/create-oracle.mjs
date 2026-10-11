// Oracle for the "Criar" (+) menu rules: the UNMODIFIED 1.8.4 app.js functions (last definition
// wins) evaluated in Node vm over a scenario {water, regions, buildings, names, rects}.
// Prints {names:[unique name per typed], draws:[{rect, mask:{cells,dry,total}, regions:[{id,cells,x,y,w,h}], newRegion:{...}, root:[ids]}]}.
// Test-only: JavaScript never runs inside the native app.
//   node create-oracle.mjs <apps/urbe/src> '<scenario json>'
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';

const [src, scenarioJson] = process.argv.slice(2);
const app = readFileSync(`${src}/app.js`, 'utf8');
function fn(name) {
  const i = app.lastIndexOf(`function ${name}(`);
  if (i < 0) throw new Error('not found: ' + name);
  let j = app.indexOf('{', i), depth = 0;
  for (; j < app.length; j++) { if (app[j] === '{') depth++; else if (app[j] === '}' && --depth === 0) break; }
  return app.slice(i, j + 1);
}
const sc = JSON.parse(scenarioJson);
const water = new Set(sc.water.map(([x, y]) => x + ',' + y));
const code = [
  'var K=function(x,y){return x+","+y};', 'var LOTE_GAP=2;',
  // final ehAgua: !MUNDO.buildable && !urbeAssentado (a tile under a building is settled; no roads in the scenario)
  'var ehAgua=function(x,y){return water.has(x+","+y)&&!world.buildings.some(function(b){return x>=b.x&&x<b.x+b.w&&y>=b.y&&y<b.y+b.h})};',
  ...['regionHasTile', 'urbeDentroDe', 'v20TileProtegidoPorConstrucaoAlheia', 'v20TileEmSubregiaoAlheia', 'v20LimitarBoundsAConstrucoes',
    'v20MascaraPrioritaria', 'v20RecalcularBounds', 'v20CederTerritorio', 'construcaoTocaRegiao', 'v21UniqueRegionName'].map(fn)
].join('\n');
const mk = r => ({ id: r.id, name: r.name, parentId: r.parentId ?? null, x: r.x, y: r.y, w: r.w, h: r.h, cells: r.cells ? r.cells.map(([x, y]) => x + ',' + y) : null });
const out = { names: [], draws: [] };
const base = () => {
  const world = { regions: sc.regions.map(mk), buildings: sc.buildings.map(b => ({ ...b })) };
  for (const r of world.regions) if (r.cells) r._cellSet = new Set(r.cells);
  const box = { world, water, Set, Math };
  runInNewContext(code, box);
  return box;
};
{
  const box = base();
  for (const t of sc.names) out.names.push(runInNewContext('v21UniqueRegionName(t)', Object.assign(box, { t })));
}
for (const [x, y, w, h] of sc.rects) {
  const box = base();
  const m = runInNewContext('v20MascaraPrioritaria({x:x,y:y,w:w,h:h},null)', Object.assign(box, { x, y, w, h }));
  const entry = { rect: [x, y, w, h], mask: { cells: m.cells.map(k => k.split(',').map(Number)), dry: m.secos, total: m.total }, regions: [], newRegion: null, root: [] };
  if (m.cells.length) {
    // app.js confirmRegion (2275): the region with the mask bounds, then v20CederTerritorio
    const r = { id: 'NEW', kind: 'region', x: m.bounds.x, y: m.bounds.y, w: m.bounds.w, h: m.bounds.h, cells: m.cells, name: 'n', description: '', color: '#fff', parentId: null };
    r._cellSet = new Set(r.cells); box.world.regions.push(r);
    runInNewContext('v20CederTerritorio(r)', Object.assign(box, { r }));
    entry.root = box.world.buildings.filter(b => !b.regionId && runInNewContext('construcaoTocaRegiao(b,r)', Object.assign(box, { b, r }))).map(b => b.id);
  }
  entry.regions = box.world.regions.map(r => ({ id: r.id, cells: r.cells ? r.cells.map(k => k.split(',').map(Number)) : null, x: r.x, y: r.y, w: r.w, h: r.h }));
  out.draws.push(entry);
}
process.stdout.write(JSON.stringify(out));
