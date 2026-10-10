// UC-19 oracle: the UNMODIFIED 1.8.4-beta terrain.js + pixel-art.js on the real 'urbe'
// world (app.js: createWorld('urbe', {spawnX: 36, spawnY: 25})). Prints, per chunk, the
// near ground (chunkPixels) and the far ground with canopies (chunkFarPixels), base64.
// With "biomes:x0,y0,x1,y1" it also prints the biome id of every tile in that rectangle.
// Test-only: JavaScript never runs inside the native app.
//   node world-chunks-oracle.mjs <apps/urbe/src/world> cx,cy [cx,cy ...] [biomes:x0,y0,x1,y1]
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';

const [dir, ...chunks] = process.argv.slice(2);
if (!dir || chunks.length === 0) throw new Error('usage: world-chunks-oracle.mjs <src/world> cx,cy ...');
const ctx = { Uint8ClampedArray, Uint8Array, Float32Array, Float64Array, Int32Array, Uint32Array, Int16Array,
  Uint16Array, Int8Array, Map, Set, Math, Array, Object, String, JSON, Number, Infinity, NaN, isFinite };
ctx.window = ctx; ctx.globalThis = ctx; ctx.self = ctx;
for (const f of ['terrain.js', 'pixel-art.js'])
  runInNewContext(readFileSync(`${dir}/${f}`, 'utf8'), ctx, { filename: f, timeout: 60000 });
const world = ctx.UrbeTerrain.createWorld('urbe', { spawnX: 36, spawnY: 25 });
const out = {};
for (const c of chunks) {
  if (c.startsWith('biomes:')) {
    const [x0, y0, x1, y1] = c.slice(7).split(',').map(Number), b = new Uint8Array((x1 - x0) * (y1 - y0));
    for (let y = y0; y < y1; y++) for (let x = x0; x < x1; x++) b[(y - y0) * (x1 - x0) + (x - x0)] = world.biome(x, y);
    out.biomes = Buffer.from(b).toString('base64');
    continue;
  }
  const [cx, cy] = c.split(',').map(Number);
  const ground = ctx.UrbeArt.chunkPixels(world, cx, cy);
  const far = ctx.UrbeArt.chunkFarPixels(world, cx, cy, ground);
  out[c] = { ground: Buffer.from(ground).toString('base64'), far: Buffer.from(far).toString('base64') };
}
process.stdout.write(JSON.stringify(out));
