// UC-19 oracle: deleting FOLDERS from Notas in the UNMODIFIED Urbe 1.8.4-beta (src/explorer/mobile-ui.js
// deleteSelected → explorer.deleteFolder, operations.js removeFolder): a folder row held 430 ms is selected, the bar
// counts "1 pasta" and only Mover para a lixeira stays enabled; confirming sends every note under the folder to the
// trash (trash.trash), forgets the folder and app.js 'explorer:folderRemoved' → apagarRegiao removes the neighbourhood
// and its sub-neighbourhoods; the sync then removes the emptied directories. A second run deletes a folder and a
// note together ("Excluir 1 pasta e 1 nota?"). Every file of the vault, the map, the trash and the folder markers
// are recorded after each step. Math.random is a fixed LCG; Date.now() and new Date() read window.__now.
// Test-only tool: never part of the native app.
//
//   PLAYWRIGHT_MODULE=/opt/node22/lib/node_modules/playwright/index.mjs \
//   node csharp/tools/legacy-folder-delete-oracle.mjs > csharp/tests/fixtures/legacy-city/folder-delete-run-a.json
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
  'Comece.md': '# Comece\n\nVeja [[Plano]] e [[Ideias]].\n',
  'Diário.md': '# Diário\n\nHoje pensei em [[Ideias]]. #diario\n',
  'Projetos/Plano.md': '---\ntags: [meta, plano]\n---\n# Plano\n\nPassos em [[Tarefas]]. Volta para [[Comece]].\n',
  'Projetos/Tarefas.md': '# Tarefas\n\n- revisar o [[Plano]]\n',
  'Área/Ideias.md': '# Ideias\n\nLer [[Leituras]].\n',
  'Área/Leituras.md': '# Leituras\n\nNada ainda.\n',
  'Área/Antigas/Velha.md': '# Velha\n\nDe [[Ideias]].\n'
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

// every entry of the vault "Urbe" in the app's own storage, as written by workspace.js
async function readVault(page) {
  return page.evaluate(async () => {
    const db = await new Promise((ok, err) => { const r = indexedDB.open('knowledge-city'); r.onsuccess = () => ok(r.result); r.onerror = () => err(r.error); });
    const out = await new Promise((ok, err) => {
      const t = db.transaction('fs', 'readonly'), s = t.objectStore('fs'), keys = s.getAllKeys(), vals = s.getAll();
      t.oncomplete = () => ok(keys.result.map((k, i) => [k, vals.result[i]])); t.onerror = () => err(t.error);
    });
    db.close();
    const files = {};
    for (const [k, v] of out) if (typeof k === 'string' && k.startsWith('Urbe/') && typeof v === 'string') files[k.slice(5)] = v;
    return files;
  });
}

async function record(page) {
  const state = await page.evaluate(() => {
    const world = UrbeCore.service('diagnostics.world').legacy();
    const docs = UrbeCore.service('documents');
    return {
      now: Date.now(),
      documents: docs.list().map(d => ({ path: d.path, id: d.id, title: d.title, content: d.content })),
      regions: world.regions.map(r => ({ id: r.id, uid: r.uid || null, name: r.name, parentId: r.parentId || null, color: r.color,
        x: r.x, y: r.y, w: r.w, h: r.h, cells: r.cells ? r.cells.slice() : null })),
      buildings: world.buildings.map(b => ({ id: b.id, path: b.tipo === 'nota' ? ((docs.get(b.documentId) || {}).path || null) : null,
        tipo: b.tipo, name: b.name, ext: b.ext || null, regionId: b.regionId || null, x: b.x, y: b.y, w: b.w, h: b.h, sprite: b.sprite })),
      camera: (c => ({ x: c.x, y: c.y, z: c.z }))(UrbeCore.service('world.life.host').camera())
    };
  });
  const files = await readVault(page);
  state.mapaWritten = files['.urbe/mapa.json'] ?? null;
  state.trashWritten = files['.urbe/trash.v2.json'] ?? null;
  state.explorer = await page.evaluate(() => localStorage.getItem('urbe.explorer.v2'));
  state.folderMarkers = Object.keys(files).filter(p => p.endsWith('.pasta')).sort();
  state.files = Object.fromEntries(Object.keys(files).filter(p => !p.startsWith('.urbe/') && !p.endsWith('.pasta'))
    .sort((x, y) => (x < y ? -1 : x > y ? 1 : 0)).map(p => [p, files[p]]));
  return state;
}

// the client point of the centre of a tile (w2s + the canvas rectangle)
async function tilePoint(page, tx, ty) {
  return page.evaluate(([tx, ty]) => {
    const cv = document.getElementById('game'), r = cv.getBoundingClientRect();
    const c = UrbeCore.service('world.life.host').camera();
    return { x: (tx * 32 + 16 - c.x) * c.z + r.width / 2 + r.left, y: (ty * 32 + 16 - c.y) * c.z + r.height / 2 + r.top };
  }, [tx, ty]);
}

// hold a row of Notas (pointerdown, 430 ms, pointerup, then the click the long press silences)
async function hold(page, id) {
  await page.evaluate(id => {
    const el = document.querySelector('.ume-row[data-id="' + id + '"]');
    if (!el) throw new Error('no row ' + id);
    el.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, pointerType: 'touch', pointerId: 3, clientX: 5, clientY: 5 }));
  }, id);
  await page.waitForTimeout(600);
  await page.evaluate(id => {
    const el = document.querySelector('.ume-row[data-id="' + id + '"]');
    el.dispatchEvent(new PointerEvent('pointerup', { bubbles: true, pointerType: 'touch', pointerId: 3 }));
    el.click();
  }, id);
}
const tap = (page, id) => page.evaluate(id => document.querySelector('.ume-row[data-id="' + id + '"]').click(), id);
const idOf = (page, path) => page.evaluate(path => UrbeCore.service('documents').get(path).id, path);

try {
  const files = { ...notes, '.urbe/tutorial.json': JSON.stringify({ versao: 'oracle', em: '2025-10-09T00:00:00.000Z' }) };
  const s = await boot(files, Object.keys(notes).length);
  const open = await record(s.page);
  const steps = {};
  await s.page.evaluate(() => UrbeCore.commands.execute('ui.explorer.open'));
  const bar = () => s.page.evaluate(() => ({ count: document.querySelector('.ume-actions [data-count]').textContent,
    disabled: ['open', 'favorite', 'move', 'compose', 'duplicate', 'delete'].filter(k => { const b = document.querySelector('.ume-actions [data-' + k + ']'); return b && b.disabled; }) }));

  // 1. Área (with the sub-folder Antigas) held → Mover para a lixeira → Excluir
  await s.page.evaluate(T => { window.__now = T; }, T0 + 30000);
  await hold(s.page, 'folder:Área');
  const bar1 = await bar();
  await s.page.click('.ume-actions [data-delete]');
  await s.page.waitForSelector('.udlg [data-primary]', { timeout: 10000 });
  const dialog1 = await s.page.evaluate(() => document.querySelector('.udlg').innerText);
  await s.page.click('.udlg [data-primary]');
  await settle(s.page);
  steps.folder = { folders: ['Área'], paths: [], bar: bar1, dialog: dialog1, ...(await record(s.page)) };

  // 2. Projetos held + Comece tapped → "Excluir 1 pasta e 1 nota?"
  await s.page.evaluate(T => { window.__now = T; }, T0 + 60000);
  await hold(s.page, 'folder:Projetos');
  await tap(s.page, await idOf(s.page, 'Comece.md'));
  const bar2 = await bar();
  await s.page.click('.ume-actions [data-delete]');
  await s.page.waitForSelector('.udlg [data-primary]', { timeout: 10000 });
  const dialog2 = await s.page.evaluate(() => document.querySelector('.udlg').innerText);
  await s.page.click('.udlg [data-primary]');
  await settle(s.page);
  steps.mixed = { folders: ['Projetos'], paths: ['Comece.md'], bar: bar2, dialog: dialog2, ...(await record(s.page)) };
  await s.context.close();
  process.stdout.write(JSON.stringify({
    source: 'apps/urbe 1.8.4-beta, vault "Urbe" in IndexedDB, Math.random LCG(0x12345678), Date.now/new Date = window.__now',
    files: Object.fromEntries(Object.keys(notes).sort((x, y) => (x < y ? -1 : x > y ? 1 : 0)).map(p => [p, notes[p]])),
    open, steps
  }) + '\n');
} finally {
  await browser.close();
  server.close();
}
