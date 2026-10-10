// UC-19: isolated Node oracle for the unmodified 1.8.4-beta pixel-art.js.
// Executes only in tests, NEVER inside MAUI/Android or distributed runtime.
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';

const sourcePath = process.argv[2];
if (!sourcePath) throw new Error('Missing path to original pixel-art.js');
const global = {};
runInNewContext(readFileSync(sourcePath, 'utf8'), {
  window: global, Uint8ClampedArray, Uint8Array, Float32Array,
  Math, Array, Object, String, JSON
}, { filename: sourcePath, timeout: 5000 });

if (!global.UrbeArt || global.UrbeArt.PX !== 16)
  throw new Error('Original UrbeArt module unavailable');

const posMod = (n, m) => ((n % m) + m) % m;
function biome(id, x, y) {
  switch (id) {
    case 'deep': return 0;
    case 'coast': return x < 8 ? 1 : 6;
    case 'mixed':
      return [5, 6, 7, 13, 10, 14, 15, 12, 18][posMod(x + y * 2, 9)];
    case 'negative': return x < -24 ? 3 : 17;
    default: throw new Error('Unexpected fixture ' + id);
  }
}
function elevation(id, x, y) {
  switch (id) {
    case 'deep': return 0.1;
    case 'coast': return x < 8 ? 0.34 : 0.56;
    case 'mixed': return 0.42 + (x % 7) * 0.007 - (y % 5) * 0.004;
    case 'negative': return x < -24 ? 0.38 : 0.58;
    default: throw new Error('Unexpected fixture ' + id);
  }
}
function decor(id, x, y) {
  if (id === 'coast' && x === 9 && y === 7) return ['flowers', 'tallgrass'];
  if (id === 'mixed' && x === 20 && y === -5) return ['fern', 'rock'];
  if (id === 'negative' && x === -20 && y === 19) return ['drygrass'];
  return null;
}

const fixtures = [
  ['deep', 0, 0], ['coast', 0, 0],
  ['mixed', 1, -1], ['negative', -2, 1]
];
const images = {};
for (const [id, cx, cy] of fixtures) {
  const world = {
    CH: 16, sea: 0.42,
    biome: (x, y) => biome(id, x, y),
    elevation: (x, y) => elevation(id, x, y),
    decor: (x, y) => decor(id, x, y)
  };
  const pixels = global.UrbeArt.chunkPixels(world, cx, cy);
  if (pixels.length !== 256 * 256 * 4)
    throw new Error('Unexpected chunk size in ' + id);
  images[id] = Buffer.from(pixels).toString('base64');
}
process.stdout.write(JSON.stringify(images));
