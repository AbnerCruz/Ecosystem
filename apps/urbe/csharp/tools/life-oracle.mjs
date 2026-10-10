// UC-19 oracle for the life of the world: the UNMODIFIED 1.8.4-beta src/world/life.js and
// the app.js fauna block ('fauna: ovelhas, vacas, cervos, patos e pássaros'), run by Node on
// a small scripted world with a seeded Math.random. Prints the world it used, the scripted
// steps and the full particle state at checkpoints, so the C# port can replay the same
// steps and compare. The only addition is a read-only _dump() on the life object.
// Test-only: JavaScript never runs inside the native app.
//   node life-oracle.mjs <apps/urbe/src> <dia|noite|entardecer> <steps> <seed>
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';

const [src, mode = 'dia', stepsArg = '1500', seedArg = '7'] = process.argv.slice(2);
if (!src) throw new Error('usage: life-oracle.mjs <apps/urbe/src> <mode> <steps> <seed>');
const STEPS = +stepsArg;

// app.js rng (mulberry32), the same generator as LegacyJsMath.Rng
function rng(s) { let a = s >>> 0; return () => { a = (a + 0x6D2B79F5) >>> 0; let t = Math.imul(a ^ (a >>> 15), 1 | a); t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t; return ((t ^ (t >>> 14)) >>> 0) / 4294967296 }; }
function hash(x, y) { let h = (Math.imul(x | 0, 374761393) + Math.imul(y | 0, 668265263)) | 0; h = Math.imul(h ^ (h >>> 13), 1274126177); return (h ^ (h >>> 16)) >>> 0; }

// ---- the scripted world (tiles)
const AREA = { x0: -12, y0: -12, x1: 64, y1: 48 }, DEFAULT_BIOME = 5;
const biomes = [];
for (let y = AREA.y0; y < AREA.y1; y++) for (let x = AREA.x0; x < AREA.x1; x++) {
  let b;
  if ((x - 22) ** 2 + (y - 13) ** 2 < 40) b = 3;                 // lake
  else if (x >= 40 && x < 44) b = 2;                              // river
  else if (x < 6) b = mode === 'noite' && y > 20 ? 12 : 7;        // forest (snow at night south-west)
  else if (y > 24) b = 17;                                        // savanna
  else b = [5, 6, 13, 8, 10][hash(x * 3 + 7, y * 5 + 11) % 5];   // grass, meadow, hills, dense, taiga
  biomes.push(b);
}
const biome = (x, y) => x < AREA.x0 || x >= AREA.x1 || y < AREA.y0 || y >= AREA.y1 ? DEFAULT_BIOME : biomes[(y - AREA.y0) * (AREA.x1 - AREA.x0) + (x - AREA.x0)];
const roads = [];
for (let x = 0; x <= 38; x++) roads.push([x, 10]);
for (let y = 0; y <= 30; y++) if (y !== 10) roads.push([30, y]);
const buildings = [[12, 6], [16, 6], [24, 18], [33, 4], [8, 11], [26, 26]].map(([x, y]) => ({ x, y, w: 3, h: 3 }));
const regions = [
  { name: 'Centro', parentId: null, x: 10, y: 4, w: 10, h: 6, cx: 15, cy: 7 },
  { name: 'Vila', parentId: 'r0', x: 23, y: 17, w: 5, h: 5, cx: 25.5, cy: 19.5 },
  { name: 'Vazio', parentId: null, x: 0, y: 0, w: 0, h: 0, cx: 0, cy: 0 }
];
// villagers: scripted (no randomness), recomputed before every step
function people(k) {
  return [
    { tipo: 'andarilho', x: 4 + ((k * .07) % 30), y: 10.4, dx: 1, dy: 0, pausa: 0, rota: [1, 2, 3], s: 1, conversa: -5, look: { shirt: '#aabbcc' } },
    { tipo: 'andarilho', x: 12.5, y: 2 + ((k * .05) % 20), dx: 0, dy: 1, pausa: k % 400 < 120 ? 1 : 0, rota: [1, 2, 3], s: k % 400 < 120 ? 0 : 1, conversa: -5, look: { shirt: '#aabbcc' } },
    { tipo: 'ocioso', x: 25, y: 20, dx: 0, dy: 0, pausa: 1, rota: [1, 2, 3], s: 2, conversa: -5, look: {} },
    { tipo: 'andarilho', x: 0, y: 0, dx: 0, dy: 0, pausa: 0, rota: null, s: 0, conversa: -5, look: {} }
  ];
}
const camera = { x: 20 * 32, y: 14 * 32, z: 1 }, W = 1280, H = 900;
const events = mode === 'noite'
  ? [[30, 'festa'], [200, 'raposa'], [260, 'meteoros'], [400, 'chuva'], [700, 'barco'], [900, 'revoada'], [1100, 'neblina']]
  : [[30, 'festa'], [150, 'balao'], [300, 'barco'], [420, 'revoada'], [600, 'chuva'], [1000, 'neblina'], [1200, 'raposa']];

// ---- the sandbox
const R = rng(+seedArg);
const sandboxMath = Object.create(Math); sandboxMath.random = R;
let nowMs = 1000, scheduled = {};
const ctx = { Math: sandboxMath, Map, Set, Array, Object, String, JSON, Number, Infinity, NaN, isFinite, console };
ctx.window = ctx; ctx.globalThis = ctx;
ctx.performance = { now: () => nowMs };
ctx.Date = class extends Date { static now() { return 1760000000000 } };
const K = (x, y) => x + ',' + y;
const world = { roads: new Set(roads.map(([x, y]) => K(x, y))), buildings, regions };
let povo = people(0);
const opcoes = { moradores: true, fauna: true, clima: true, eventos: true, nomes: true, bairros: true, ambiente: mode, editor: 'visual' };
const bAt = p => buildings.find(b => p.x >= b.x && p.x < b.x + b.w && p.y >= b.y && p.y < b.y + b.h) || null;
const regAt = p => regions.find(r => r.w && p.x >= r.x && p.x < r.x + r.w && p.y >= r.y && p.y < r.y + r.h) || null;
const faixa = () => {
  const s2w = (x, y) => ({ x: (x - W / 2) / camera.z + camera.x, y: (y - H / 2) / camera.z + camera.y });
  const a = s2w(0, 0), b = s2w(W, H);
  return { x0: Math.floor(a.x / 32) - 1, y0: Math.floor(a.y / 32) - 2, x1: Math.ceil(b.x / 32) + 1, y1: Math.ceil(b.y / 32) + 2 };
};
const services = {
  scheduler: { add: (id, fn) => { scheduled[id] = fn } },
  'world.life.host': {
    ctx: {}, TILE: 32, camera: () => camera, w2s: (x, y) => ({ x: (x - camera.x) * camera.z + W / 2, y: (y - camera.y) * camera.z + H / 2 }),
    faixa, largura: () => W, altura: () => H,
    agua: (x, y) => biome(Math.floor(x), Math.floor(y)) <= 3,
    bioma: (x, y) => biome(Math.floor(x), Math.floor(y)),
    B: { DEEP: 0, SEA: 1, RIVER: 2, LAKE: 3, BEACH: 4, GRASS: 5, MEADOW: 6, FOREST: 7, DENSE: 8, SWAMP: 9, TAIGA: 10, TUNDRA: 11, SNOW: 12, HILLS: 13, MOUNTAIN: 14, PEAK: 15, DESERT: 16, SAVANNA: 17, STEPPE: 18 },
    mundo: () => world, rua: (x, y) => world.roads.has(K(Math.floor(x), Math.floor(y))),
    casaEm: (x, y) => bAt({ x: Math.floor(x), y: Math.floor(y) }),
    povo: () => povo, fauna: () => ctx.urbeFauna, opcoes: () => opcoes,
    animar() { }, redesenhar() { }, visivel: () => true, pressa(f) { ctx.pressa = f },
    nomeBairro: r => r && r.name || '', centroBairro: r => ({ x: r.cx, y: r.cy }),
    registrar(v) { ctx.vida = v }
  }
};
ctx.UrbeCore = { service: id => services[id] };
Object.assign(ctx, {
  MUNDO: { B: services['world.life.host'].B, CH: 64, has: () => true, biome },
  ARTE: { animal() { } }, world, K, bAt, regAt, camera, faixaVisivel: faixa, urbeOpcoes: opcoes,
  pedirDesenho() { }, pedirAnimacao() { }, v25MapaVisivel: () => true
});

// app.js fauna block, verbatim (from 'var urbeOpcoes=' to the end of its IIFE)
const app = readFileSync(`${src}/app.js`, 'utf8');
const a = app.indexOf('var urbeOpcoes={'), b = app.indexOf("sch.add('world.fauna'", a), end = app.indexOf('})();', b) + 5;
if (a < 0 || b < 0) throw new Error('fauna block not found in app.js');
runInNewContext(app.slice(a, end).replace('var urbeOpcoes=', 'var urbeOpcoesOriginal='), ctx, { filename: 'app.js#fauna' });
// world/life.js, verbatim plus a read-only state dump
let life = readFileSync(`${src}/world/life.js`, 'utf8');
const dump = '_dump:function(){return{T:T,vento:vento,chuva:chuva,neblina:neblina,arco:arco,balao:balao,barco:barco,raposa:raposa,cao:cao&&{x:cao.x,y:cao.y,fr:cao.fr,flip:cao.flip,senta:cao.senta,troca:cao.troca},festa:festa,meteoros:meteoros,' +
  'nuvens:nuvens,neblinas:neblinas,gotas:gotas,respingos:respingos,ondas:ondas,brilhos:brilhos,peixes:peixes,borboletas:borboletas,vagalumes:vagalumes,fumaca:fumaca,pombos:pombos,folhas:folhas,rajadas:rajadas,' +
  'baloes:baloes,confete:confete,foguetes:foguetes,faiscas:faiscas,claroes:claroes,estrelas:estrelas,emotes:Array.from(emotes.entries()).map(function(e){return[H.povo().indexOf(e[0]),e[1].c,e[1].fim]}),lampioes:listaLampioes(),luz:LUZ}},';
if (!life.includes('var vida={chao:chao,')) throw new Error('life.js changed: vida object not found');
life = life.replace('var vida={chao:chao,', 'var vida={' + dump + 'chao:chao,');
runInNewContext(life, ctx, { filename: 'life.js' });

const checkpoints = [];
const DT = .033;
for (let k = 1; k <= STEPS; k++) {
  people(k).forEach((p, i) => Object.assign(povo[i], p));  // same objects, as v25Povo
  for (const [at, kind] of events) if (at === k) ctx.vida.evento(kind);
  nowMs += 33;
  scheduled['world.fauna']();
  ctx.vida._passo(DT);
  if (k % 50 === 0 || k === STEPS) {
    const d = JSON.parse(JSON.stringify(ctx.vida._dump()));
    d.k = k; d.pressa = ctx.pressa;
    d.fauna = ctx.urbeFauna.map(f => ({ kind: f.kind, x: f.x, y: f.y, tx: f.tx, ty: f.ty, st: f.st, t: f.t, fr: f.fr, ft: f.ft, flip: f.flip, alt: f.alt }));
    checkpoints.push(d);
  }
}
process.stdout.write(JSON.stringify({
  mode, seed: +seedArg, dt: DT, steps: STEPS, startMs: 1000, area: AREA, defaultBiome: DEFAULT_BIOME,
  biomes: Buffer.from(Uint8Array.from(biomes)).toString('base64'), roads, buildings, regions, camera, width: W, height: H,
  view: faixa(), events, checkpoints
}));
