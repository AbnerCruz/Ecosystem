// UC-19 oracle: the streets after an edit in the UNMODIFIED Urbe 1.8.4-beta (apps/urbe) in Chromium.
// markChanged / syncVisualToMarkdown (app.js ≈1083/≈707): when the note's [[link]] signature changes,
// scheduleRoadRebuild (120 ms) → rebuildRoadNetwork, unless the RoadGraph (world/roads.js) has no edge.
// Records world.roads/world.links right after each edit and after the save settles.
// Test-only tool: never part of the native app.
//
//   PLAYWRIGHT_MODULE=/opt/node22/lib/node_modules/playwright/index.mjs CHROMIUM_PATH=/opt/pw-browsers/chromium/chrome-linux/chrome \
//   node csharp/tools/legacy-roads-oracle.mjs > csharp/tests/fixtures/legacy-city/roads-edit-run-a.json
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
  'Área/Leituras.md': '# Leituras\n\nNada ainda.\n'
};
const tutorialMarker = JSON.stringify({ versao: 'oracle', em: '2025-10-09T00:00:00.000Z' });
const T0 = 1760000000000;
const STEPS_UNUSED = [
  { key: 'rename', from: 'Projetos/Tarefas.md', typed: 'Lista de tarefas' },
  { key: 'collision', from: 'Área/Leituras.md', typed: 'Ideias' },
  { key: 'extension', from: 'Comece.md', typed: 'Comece.txt' },
  { key: 'case', from: 'Diário.md', typed: 'diário' }
];

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
  state.files = Object.fromEntries(Object.keys(files).filter(p => !p.startsWith('.urbe/'))
    .sort((x, y) => (x < y ? -1 : x > y ? 1 : 0)).map(p => [p, files[p]]));
  return state;
}


async function roads(page) {
  return page.evaluate(() => {
    const world = UrbeCore.service('diagnostics.world').legacy();
    const docs = UrbeCore.service('documents');
    const pathOf = id => { const b = world.buildings.find(x => x.id === id); return b ? ((docs.get(b.documentId) || {}).path || b.name) : id; };
    return {
      roads: Array.from(world.roads).sort(),
      buildings: world.buildings.map(b => [pathOf(b.id), b.x, b.y]),
      links: world.links.map(l => [pathOf(l.from), pathOf(l.to)].sort().join(' | ')).sort()
    };
  });
}

const EDITS = [
  { key: 'addLink', path: 'Área/Leituras.md', text: '# Leituras\n\nNada ainda. Ver [[Diário]].\n' },
  { key: 'removeLinks', path: 'Comece.md', text: '# Comece\n\nSem links agora.\n' },
  { key: 'sameLinks', path: 'Diário.md', text: '# Diário\n\nOutro texto com [[Ideias]]. #diario\n' }
];

try {
  const files = { ...notes, '.urbe/tutorial.json': tutorialMarker };
  const s = await boot(files, Object.keys(notes).length);
  const open = await roads(s.page);
  const vault = await readVault(s.page);
  open.mapaWritten = vault['.urbe/mapa.json'];
  open.documents = await s.page.evaluate(() => UrbeCore.service('documents').list().map(d => ({ path: d.path, id: d.id })));
  open.now = await s.page.evaluate(() => Date.now());
  const steps = {};
  let t = T0;
  for (const e of EDITS) {
    t += 30000;
    await s.page.evaluate(T => { window.__now = T; }, t);
    await s.page.evaluate(path => UrbeCore.commands.execute('document.open', { path }), e.path);
    await s.page.waitForTimeout(500);
    await s.page.evaluate(() => { if (typeof setEditorViewMode === 'function') setEditorViewMode('source'); });
    await s.page.evaluate(text => { const ed = document.getElementById('bodyEditor'); ed.value = text; ed.dispatchEvent(new Event('input', { bubbles: true })); }, e.text);
    await s.page.waitForTimeout(400);
    const early = await roads(s.page);
    await settle(s.page);
    const late = await roads(s.page);
    await s.page.evaluate(() => { if (typeof closeFullEditor === 'function') closeFullEditor(); });
    await settle(s.page);
    steps[e.key] = { ...e, early, late, closed: await roads(s.page) };
  }
  await s.context.close();
  process.stdout.write(JSON.stringify({ source: 'apps/urbe 1.8.4-beta, vault "Urbe" in IndexedDB, Math.random LCG(0x12345678)',
    files: Object.fromEntries(Object.keys(notes).sort((x, y) => (x < y ? -1 : x > y ? 1 : 0)).map(p => [p, notes[p]])), open, steps }) + '\n');
} finally {
  await browser.close();
  server.close();
}
