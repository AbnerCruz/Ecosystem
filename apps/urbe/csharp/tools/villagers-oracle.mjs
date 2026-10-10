// UC-19 oracle for the villagers: the UNMODIFIED 1.8.4-beta app.js v25 block (from
// 'var V25_MAX' to the 'aquarium.pedestrians' scheduler) and pixel-art.js villager(), run by
// Node on a small scripted town with a seeded Math.random and a scripted Date.now. Prints the
// town, the villagers at checkpoints and the sprite pixels of every look that appeared.
// Test-only: JavaScript never runs inside the native app.
//   node villagers-oracle.mjs <apps/urbe/src> <steps> <seed>
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';

const [src, stepsArg = '1500', seedArg = '5'] = process.argv.slice(2);
if (!src) throw new Error('usage: villagers-oracle.mjs <apps/urbe/src> <steps> <seed>');
const STEPS = +stepsArg;
function rng(s) { let a = s >>> 0; return () => { a = (a + 0x6D2B79F5) >>> 0; let t = Math.imul(a ^ (a >>> 15), 1 | a); t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t; return ((t ^ (t >>> 14)) >>> 0) / 4294967296 }; }

// ---- the scripted town (tiles)
const roads = [];
const addRoad = (x, y) => { if (!roads.some(r => r[0] === x && r[1] === y)) roads.push([x, y]); };
for (const y of [10, 20]) for (let x = 0; x <= 40; x++) addRoad(x, y);
for (const x of [5, 20, 35]) for (let y = 0; y <= 30; y++) addRoad(x, y);
const spots = [[1, 6], [8, 6], [14, 6], [24, 6], [30, 6], [8, 16], [14, 16], [26, 16], [37, 16], [0, 24]];
const buildings = spots.map(([x, y], i) => ({ id: 'b' + i + '-' + (i * 7919 % 97), kind: 'building', tipo: 'nota', x, y, w: 3, h: 3, name: 'n' + i }));
const L = [[0, 1], [0, 2], [1, 3], [2, 5], [4, 6], [6, 7], [3, 7], [5, 6], [1, 2], [8, 7]];
const links = L.map(([a, b]) => ({ from: buildings[a].id, to: buildings[b].id }));
const water = (x, y) => x >= 10 && x <= 13 && y >= 22 && y <= 25;
const camera = { x: 20 * 32, y: 14 * 32, z: 1 }, W = 1280, H = 900;
const hurry = k => k < 600 ? 1 : 1 + .7 * Math.min(1, (k - 600) / 300);

// ---- the sandbox
const R = rng(+seedArg);
const sandboxMath = Object.create(Math); sandboxMath.random = R;
let nowMs = 1760000000000, scheduled = {};
const ctx = { Math: sandboxMath, Map, Set, Array, Object, String, JSON, Number, Infinity, NaN, isFinite, console, Uint8ClampedArray, Uint8Array, Float32Array, Int32Array };
ctx.window = ctx; ctx.globalThis = ctx;
ctx.Date = class extends Date { static now() { return nowMs } };
class FakeCanvas { constructor(w, h) { this.width = w; this.height = h; const self = this; this.ctx = { createImageData: (a, b) => ({ data: new Uint8ClampedArray(a * b * 4) }), putImageData(img) { self.data = img.data } }; } getContext() { return this.ctx } }
ctx.OffscreenCanvas = FakeCanvas;
const K = (x, y) => x + ',' + y;
const world = { roads: new Set(roads.map(([x, y]) => K(x, y))), buildings, links, regions: [] };
const faixa = () => {
  const s2w = (x, y) => ({ x: (x - W / 2) / camera.z + camera.x, y: (y - H / 2) / camera.z + camera.y });
  const a = s2w(0, 0), b = s2w(W, H);
  return { x0: Math.floor(a.x / 32) - 1, y0: Math.floor(a.y / 32) - 2, x1: Math.ceil(b.x / 32) + 1, y1: Math.ceil(b.y / 32) + 2 };
};
Object.assign(ctx, {
  UrbeCore: { service: id => id === 'scheduler' ? { add: (n, fn) => { scheduled[n] = fn } } : null },
  world, K, faixaVisivel: faixa, camera,
  bAt: p => buildings.find(b => p.x >= b.x && p.x < b.x + b.w && p.y >= b.y && p.y < b.y + b.h) || null,
  MUNDO: { isWater: water },
  lowerRoadStart(b) { const x = Math.round(b.x + (b.w - 1) / 2); const y = b.y + b.h + 1; return { x, y }; },
  drawTrees() { }, pedirAnimacao() { }, pedirDesenho() { }, rebuildRoadNetwork() { }
});
const app = readFileSync(`${src}/app.js`, 'utf8');
const a = app.indexOf('var V25_MAX='), b = app.indexOf("add('aquarium.pedestrians'", a), end = app.indexOf('})();', b) + 5;
if (a < 0 || b < 0) throw new Error('v25 block not found in app.js');
runInNewContext(app.slice(a, end), ctx, { filename: 'app.js#v25' });
runInNewContext(readFileSync(`${src}/world/pixel-art.js`, 'utf8'), ctx, { filename: 'pixel-art.js' });

const checkpoints = [], looks = new Map();
for (let k = 1; k <= STEPS; k++) {
  ctx.urbeVelPovo = hurry(k);
  nowMs += 33;
  scheduled['aquarium.pedestrians']();
  for (const p of ctx.v25Povo) looks.set(JSON.stringify(p.look), p.look);
  if (k % 50 === 0 || k === STEPS)
    checkpoints.push({
      k, people: ctx.v25Povo.map(p => ({
        tipo: p.tipo, de: buildings.indexOf(p.de), para: p.para ? buildings.indexOf(p.para) : -1, rota: p.rota || null,
        x: p.x, y: p.y, s: p.s ?? 0, sentido: p.sentido ?? 1, pausa: p.pausa, passo: p.passo, dx: p.dx, dy: p.dy, conversa: p.conversa, vel: p.vel, look: p.look
      }))
    });
}
const sprites = [];
for (const look of looks.values())
  for (const dir of ['down', 'up', 'side'])
    for (let fr = 0; fr < 4; fr++)
      sprites.push({ look, dir, frame: fr, rgba: Buffer.from(ctx.UrbeArt.villager(look, dir, fr).data).toString('base64') });
process.stdout.write(JSON.stringify({ seed: +seedArg, steps: STEPS, startMs: 1760000000000, roads, buildings, links: L, camera, width: W, height: H, view: faixa(), checkpoints, sprites }));
