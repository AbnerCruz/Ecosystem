// Reproduce, do not redraw: export the exact 1.8.4-beta canvas sprite bytes.
// This runs at BUILD TIME ONLY in headless Chromium. No legacy JavaScript is
// shipped or executed as the Android app's domain/rendering runtime.
import { chromium } from 'playwright';
import { createHash } from 'node:crypto';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const here = path.dirname(fileURLToPath(import.meta.url));
const legacy = path.resolve(here, '../../src/world/pixel-art.js');
const manifest = path.resolve(here, '../../package.json');
const out = path.resolve(here, '../src/Urbe.UI/wwwroot/world/v184');
const source = await readFile(legacy);
const pkg = JSON.parse(await readFile(manifest, 'utf8'));
if (pkg.version !== '1.8.4-beta')
  throw new Error('A fonte visual deixou de ser Urbe 1.8.4-beta; bloqueado.');
const sourceDigest = createHash('sha256').update(source).digest('hex');
await mkdir(out, { recursive: true });

const browser = await chromium.launch({ headless: true });
try {
  const page = await browser.newPage({ viewport: { width: 360, height: 640 } });
  await page.goto('about:blank');
  await page.addScriptTag({ path: legacy });
  const sprites = await page.evaluate(async () => {
    if (!window.UrbeArt || window.UrbeArt.PX !== 16)
      throw new Error('API da arte original ausente/incompatível.');
    const art = window.UrbeArt, result = {};
    const save = (name, canvas) => {
      // OffscreenCanvas is possible in Chromium; copy without smoothing or
      // re-encoding via CSS. The PNG retains the original source pixels.
      const target = document.createElement('canvas');
      target.width = canvas.width;
      target.height = canvas.height;
      const ctx = target.getContext('2d', { alpha: true });
      ctx.imageSmoothingEnabled = false;
      ctx.drawImage(canvas, 0, 0);
      result[name] = target.toDataURL('image/png').split(',')[1];
    };
    for (const style of ['temperate', 'cold', 'dry', 'coast', 'wet']) {
      for (const kind of ['house', 'hall', 'tower', 'workshop', 'market', 'store', 'dyer']) {
        for (let v = 0; v < 3; v++) {
          save(`building-${kind}-${style}-${v}.png`, art.building(kind, style, v, false));
        }
      }
    }
    for (const kind of ['oak', 'birch', 'willow', 'pine', 'deadpine', 'acacia', 'palm', 'cactus']) {
      for (let v = 0; v < 3; v++) {
        save(`tree-${kind}-${v}.png`, art.tree(kind, v, false));
      }
    }
    for (const biome of art.IDS) {
      for (let variant = 0; variant < 4; variant++) {
        const pixels = art.texture(biome, variant);
        const target = document.createElement('canvas');
        target.width = target.height = art.PX;
        const ctx = target.getContext('2d');
        const image = ctx.createImageData(art.PX, art.PX);
        image.data.set(pixels);
        ctx.putImageData(image, 0, 0);
        result[`terrain-${biome}-${variant}.png`] = target.toDataURL('image/png').split(',')[1];
      }
    }
    save('bridge.png', art.bridge());
    return result;
  });
  const entries = [];
  for (const [name, base64] of Object.entries(sprites)) {
    const data = Buffer.from(base64, 'base64');
    await writeFile(path.join(out, name), data);
    entries.push({ name, sha256: createHash('sha256').update(data).digest('hex') });
  }
  entries.sort((a, b) => a.name.localeCompare(b.name, 'en'));
  const report = {
    source: 'Urbe 1.8.4-beta',
    generator: 'apps/urbe/src/world/pixel-art.js',
    source_sha256: sourceDigest,
    tile_size_px: 16,
    assets: entries,
  };
  await writeFile(path.join(out, 'art-manifest.json'), JSON.stringify(report, null, 2) + '\n');
  console.log(`Urbe v1.8.4-beta: ${entries.length} bitmaps extracted from source; no redesign.`);
} finally {
  await browser.close();
}
