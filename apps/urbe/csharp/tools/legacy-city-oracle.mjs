// UC-19 oracle: runs the UNMODIFIED Urbe 1.8.4-beta (apps/urbe) in Chromium on a fresh
// vault, lets it build the Tutorial city exactly as on first open, and records the result.
// Math.random and Date.now are fixed so the original layout is reproducible; the ids it
// generates are recorded so the C# port (LegacyCity) can be fed the same ids.
// Test-only tool: never part of the native app.
//
//   node csharp/tools/legacy-city-oracle.mjs > csharp/tests/fixtures/legacy-city/tutorial.json
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { extname, join, normalize, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const { chromium } = await import(process.env.PLAYWRIGHT_MODULE || 'playwright');
const types = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.css': 'text/css',
  '.json': 'application/json', '.png': 'image/png', '.svg': 'image/svg+xml', '.webmanifest': 'application/manifest+json' };
const server = createServer(async (req, res) => {
  try {
    const path = normalize(decodeURIComponent(new URL(req.url, 'http://x').pathname)).replace(/^([/\\])+/, '');
    const body = await readFile(join(root, path || 'index.html'));
    res.writeHead(200, { 'content-type': types[extname(path)] || 'application/octet-stream' });
    res.end(body);
  } catch { res.writeHead(404); res.end(); }
});
await new Promise(r => server.listen(0, '127.0.0.1', r));
const url = `http://127.0.0.1:${server.address().port}/index.html`;

const browser = await chromium.launch(process.env.CHROMIUM_PATH ? { executablePath: process.env.CHROMIUM_PATH } : {});
try {
  const context = await browser.newContext({ viewport: { width: 1280, height: 760 }, serviceWorkers: 'block' });
  await context.route(/^https?:\/\/(?!127\.0\.0\.1)/, r => r.abort());
  await context.addInitScript(() => {
    let s = 0x12345678 >>> 0;
    Math.random = () => { s = (Math.imul(s, 1664525) + 1013904223) >>> 0; return s / 4294967296; };
    const T = 1760000000000; Date.now = () => T;
  });
  const page = await context.newPage();
  await page.goto(url, { waitUntil: 'load' });
  await page.waitForFunction(() => {
    const d = window.UrbeCore && UrbeCore.service('diagnostics.world');
    const w = d && d.legacy();
    return w && w.buildings.length >= 48 && w.roads.size > 0;
  }, null, { timeout: 60000 });
  await page.waitForTimeout(2500);
  const out = await page.evaluate(() => {
    const world = UrbeCore.service('diagnostics.world').legacy();
    const docs = UrbeCore.service('documents');
    return {
      source: 'apps/urbe 1.8.4-beta, first open, Math.random LCG(0x12345678), Date.now=1760000000000',
      documents: docs.list().map(d => d.path),
      regions: world.regions.map(r => ({ id: r.id, name: r.name, parentId: r.parentId || null, color: r.color,
        x: r.x, y: r.y, w: r.w, h: r.h, cells: r.cells.slice() })),
      buildings: world.buildings.map(b => ({ id: b.id, path: (docs.get(b.documentId) || {}).path || null,
        name: b.name, regionId: b.regionId || null, x: b.x, y: b.y })),
      roads: Array.from(world.roads),
      links: world.links.map(l => [l.from, l.to])
    };
  });
  process.stdout.write(JSON.stringify(out) + '\n');
} finally {
  await browser.close();
  server.close();
}
