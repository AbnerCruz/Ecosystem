// UC-18/UC-19 visual reference: the Notas screen of the UNMODIFIED Urbe 1.8.4-beta (src/explorer/mobile-ui.js) rendered in
// Chromium on the vault of the native test Notes_IsTheExpandableTreeOfTheOriginal (Bairro and Sub open), at 412×860 (tree,
// then a held note with the selection bar) and 1280×860 (the panel beside the city), with the measured boxes printed.
// Compare with the native snapshots (URBE_SNAPSHOT_DIR=… dotnet test --filter Notes_IsTheExpandable). Test-only tool.
//
//   PLAYWRIGHT_MODULE=/opt/node22/lib/node_modules/playwright/index.mjs \
//   node csharp/tools/legacy-notes-screenshot.mjs <output-folder>
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { extname, join, normalize, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const { chromium } = await import(process.env.PLAYWRIGHT_MODULE || 'playwright');
const types = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.css': 'text/css',
  '.json': 'application/json', '.png': 'image/png', '.svg': 'image/svg+xml', '.webmanifest': 'application/manifest+json' };
const server = createServer(async (req, res) => {
  if (req.url === '/__seed__') { res.writeHead(200, { 'content-type': 'text/html' }); res.end('<!doctype html><title>seed</title>'); return; }
  try {
    const path = normalize(decodeURIComponent(new URL(req.url, 'http://x').pathname)).replace(/^([/\\])+/, '');
    const body = await readFile(join(root, path || 'index.html'));
    res.writeHead(200, { 'content-type': types[extname(path)] || 'application/octet-stream' });
    res.end(body);
  } catch { res.writeHead(404); res.end(); }
});
await new Promise(r => server.listen(0, '127.0.0.1', r));
const origin = `http://127.0.0.1:${server.address().port}`;

const notes = {
  'Comece.md': '# Comece\n\n[[Um]]\n',
  'Árvore.md': '# Árvore\n',
  'Bairro/Um.md': '# Um\n',
  'Bairro/Sub/Dois.md': '# Dois\n'
};
const tutorialMarker = JSON.stringify({ versao: 'oracle', em: '2025-10-09T00:00:00.000Z' });
const T0 = 1760000000000;


const browser = await chromium.launch(process.env.CHROMIUM_PATH ? { executablePath: process.env.CHROMIUM_PATH } : {});

async function boot(files, expectedBuildings) {
  const context = await browser.newContext({ viewport: { width: 1280, height: 760 }, serviceWorkers: 'block' });
  await context.route(/^https?:\/\/(?!127\.0\.0\.1)/, r => r.abort());
  await context.addInitScript(T => {
    let s = 0x12345678 >>> 0;
    Math.random = () => { s = (Math.imul(s, 1664525) + 1013904223) >>> 0; return s / 4294967296; };
    window.__now = T;
    const RD = Date;
    function FD(...a) { if (!new.target) return RD(); return a.length ? new RD(...a) : new RD(window.__now); }
    FD.prototype = RD.prototype; FD.now = () => window.__now; FD.UTC = RD.UTC; FD.parse = RD.parse;
    window.Date = FD;
  }, T0);
  const page = await context.newPage();
  await page.goto(origin + '/__seed__', { waitUntil: 'load' });
  await page.evaluate(async entries => {
    const db = await new Promise((ok, err) => {
      const r = indexedDB.open('knowledge-city', 3);
      r.onupgradeneeded = e => { const d = e.target.result; ['kv', 'fs', 'blobs'].forEach(s => { if (!d.objectStoreNames.contains(s)) d.createObjectStore(s); }); };
      r.onsuccess = () => ok(r.result); r.onerror = () => err(r.error);
    });
    await new Promise((ok, err) => {
      const t = db.transaction('fs', 'readwrite'), s = t.objectStore('fs');
      for (const [k, v] of entries) s.put(v, 'Urbe/' + k);
      t.oncomplete = ok; t.onerror = () => err(t.error);
    });
    db.close();
  }, Object.entries(files));
  await page.goto(origin + '/index.html', { waitUntil: 'load' });
  await page.waitForFunction(n => {
    const d = window.UrbeCore && UrbeCore.service('diagnostics.world');
    const w = d && d.legacy(), p = UrbeCore.service('persistence');
    return w && p && p.vault === 'Urbe' && w.buildings.length >= n && w.roads.size > 0;
  }, expectedBuildings, { timeout: 60000 });
  await settle(page);
  return { context, page };
}

async function settle(page) {
  await page.waitForTimeout(2500);
  await page.waitForFunction(() => {
    const p = UrbeCore.service('persistence');
    return !p.timer && !p.busy && !p.pending;
  }, null, { timeout: 30000 });
  await page.waitForTimeout(300);
}


try {
  const files = { ...notes, '.urbe/tutorial.json': JSON.stringify({ versao: 'oracle', em: '2025-10-09T00:00:00.000Z' }) };
  const s = await boot(files, Object.keys(notes).length);
  await s.page.setViewportSize({ width: 412, height: 860 });
  await s.page.evaluate(() => UrbeCore.commands.execute('ui.explorer.open'));
  await s.page.waitForTimeout(500);
  const click = id => s.page.evaluate(id => document.querySelector('.ume-row[data-id="' + id + '"]').click(), id);
  // the same state as the native snapshot: Bairro and Sub open
  const st = await s.page.evaluate(() => UrbeCore.service('explorer').isExpanded('Bairro'));
  if (!st) await click('folder:Bairro');
  await s.page.waitForTimeout(200);
  if (!(await s.page.evaluate(() => UrbeCore.service('explorer').isExpanded('Bairro/Sub')))) await click('folder:Bairro/Sub');
  await s.page.waitForTimeout(400);
  await s.page.screenshot({ path: process.argv[2] + '/legacy-notes-tree.png' });
  console.log(JSON.stringify(await s.page.evaluate(() => {
    const r = sel => { const e = document.querySelector(sel); if (!e) return null; const b = e.getBoundingClientRect(); const cs = getComputedStyle(e); return [sel, Math.round(b.x), Math.round(b.y), Math.round(b.width), Math.round(b.height), cs.fontFamily.slice(0,40), cs.fontWeight, cs.fontSize]; };
    return ['#urbeMobileExplorer', '#urbeMobileExplorer>header', '#urbeMobileExplorer>header strong', '[data-new]', '[data-more]', '.ume-search', '.ume-search label', '.ume-list', '.ume-row', '.ume-row .ume-name', '#v23Dock', '#v23Dock button.ativo', '#v23Dock button'].map(r);
  })));
  console.log(JSON.stringify(await s.page.evaluate(() => [...document.querySelectorAll('#v23Dock svg')].map(x => x.getBoundingClientRect().width + 'x' + getComputedStyle(x).strokeWidth))));
  console.log(JSON.stringify(await s.page.evaluate(() => [...document.querySelectorAll('#v23Dock button')].map(b => [b.className, b.textContent.trim(), b.getBoundingClientRect().x]))));
  const cid = await s.page.evaluate(() => UrbeCore.service('documents').get('Comece.md').id);
  await s.page.evaluate(id => { const el = document.querySelector('.ume-row[data-id="' + id + '"]'); el.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, pointerType: 'touch', pointerId: 3, clientX: 5, clientY: 5 })); }, cid);
  await s.page.waitForTimeout(600);
  await s.page.evaluate(id => { const el = document.querySelector('.ume-row[data-id="' + id + '"]'); el.dispatchEvent(new PointerEvent('pointerup', { bubbles: true, pointerType: 'touch', pointerId: 3 })); el.click(); }, cid);
  await s.page.waitForTimeout(300);
  await s.page.screenshot({ path: process.argv[2] + '/legacy-notes-bar.png' });
  console.log(JSON.stringify(await s.page.evaluate(() => { const b = document.querySelector('.ume-actions').getBoundingClientRect(); const cs = getComputedStyle(document.querySelector('.ume-actions [data-open]')), bs = getComputedStyle(document.querySelector('.ume-actions')), ic = document.querySelector('.ume-actions [data-open] svg').getBoundingClientRect(), cnt = getComputedStyle(document.querySelector('.ume-actions [data-count]')); return [[cs.padding, cs.background, cs.minHeight, cs.height, cs.color, cs.borderRadius], [bs.padding, bs.gap, bs.background], [ic.width, ic.height], [cnt.fontSize, cnt.fontWeight, cnt.color, cnt.padding], b.x, b.y, b.width, b.height, [...document.querySelectorAll('.ume-actions button')].map(x => [x.getAttribute('aria-label'), Math.round(x.getBoundingClientRect().x), Math.round(x.getBoundingClientRect().width), x.disabled])]; })));
  await s.page.evaluate(() => UrbeCore.service('explorer').clear());
  await s.page.setViewportSize({ width: 1280, height: 860 });
  await s.page.waitForTimeout(600);
  await s.page.screenshot({ path: process.argv[2] + '/legacy-notes-wide.png' });
  await s.context.close();
} finally {
  await browser.close();
  server.close();
}
