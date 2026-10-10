// UC-19: oráculo isolado do sprite de morador (villager) do pixel-art.js 1.8.4-beta.
// Só roda em testes, nunca no app. Usa um canvas falso que apenas guarda o ImageData escrito.
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';

const sourcePath = process.argv[2];
if (!sourcePath) throw new Error('Missing path to original pixel-art.js');
class FakeCanvas {
  constructor(w, h) { this.width = w; this.height = h; this.data = null; }
  getContext() {
    const self = this;
    return {
      createImageData: (w, h) => ({ width: w, height: h, data: new Uint8ClampedArray(w * h * 4) }),
      putImageData: img => { self.data = img.data; }
    };
  }
}
const global = {};
runInNewContext(readFileSync(sourcePath, 'utf8'), {
  window: global, OffscreenCanvas: FakeCanvas, Uint8ClampedArray, Uint8Array, Float32Array,
  Math, Array, Object, String, JSON
}, { filename: sourcePath, timeout: 5000 });

const looks = [
  { skin: 0, hair: 0, pants: 0, style: 'short', shirt: '#b84a3a', dress: 0, acc: null },
  { skin: 4, hair: 5, pants: 4, style: 'long', shirt: '#3d6fb0', dress: 1, acc: 'basket' },
  { skin: 2, hair: 3, pants: 2, style: 'straw', shirt: '#c9a24a', dress: 0, acc: 'basket' },
  { skin: 1, hair: 1, pants: 1, style: 'bald', shirt: '#8e8f8a', dress: 0, acc: 'bucket' },
  { skin: 3, hair: 2, pants: 3, style: 'cap', cap: '#a33c32', shirt: '#5f8f4a', dress: 0, acc: 'sack' },
  { skin: 0, hair: 4, pants: 0, style: 'hood', hood: '#5b4a6a', shirt: '#e6e2d3', dress: 1, acc: 'staff' }
];
const out = [];
for (const look of looks)
  for (const dir of ['down', 'up', 'side'])
    for (let frame = 0; frame < 4; frame++) {
      const c = global.UrbeArt.villager(look, dir, frame);
      out.push({ look, dir, frame, width: c.width, height: c.height,
        rgba: Buffer.from(c.data).toString('base64') });
    }
process.stdout.write(JSON.stringify(out));
