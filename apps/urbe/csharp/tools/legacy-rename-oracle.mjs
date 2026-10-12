// UC-19 oracle: "Renomear" of a note house in the UNMODIFIED Urbe 1.8.4-beta (apps/urbe) in Chromium.
// The final renomearNota (app.js ≈2397: UD.prompt "Nome do arquivo" → v21UniqueName →
// aplicarNomeCompletoNota) only changes the house (name/ext); the FILE is moved by the next save:
// estadoDesejado → urbeReconcileWorld (≈3642) upserts the document at the house's path
// ({source:'world.move'}), and workspace.js flush writes the new path and removes the old one.
// After each rename the oracle records every file of the vault as the app left it in its own
// IndexedDB storage (the map included), so the C# port can be checked byte for byte.
//
// Math.random is a fixed LCG; Date.now() and new Date() read window.__now (as legacy-mapa-oracle.mjs).
//   steps (same session, one after the other):
//     "rename"    Projetos/Tarefas.md  typed "Lista de tarefas"  → Projetos/Lista de tarefas.md
//     "collision" Área/Leituras.md     typed "Ideias"            → Área/Ideias (2).md (v21UniqueName)
//     "extension" Comece.md            typed "Comece.txt"        → Comece.txt
//     "case"      Diário.md            typed "diário"            → diário.md (only the case changes)
// Test-only tool: never part of the native app.
//
//   PLAYWRIGHT_MODULE=/opt/node22/lib/node_modules/playwright/index.mjs CHROMIUM_PATH=/opt/pw-browsers/chromium/chrome-linux/chrome \
//   node csharp/tools/legacy-rename-oracle.mjs > csharp/tests/fixtures/legacy-city/rename-note-run-a.json
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
const STEPS = [
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

try {
  const files = { ...notes, '.urbe/tutorial.json': tutorialMarker };
  const s = await boot(files, Object.keys(notes).length);
  const open = await record(s.page);
  if (!open.mapaWritten) throw new Error('open: the app did not write .urbe/mapa.json');
  const steps = {};
  let t = T0;
  for (const step of STEPS) {
    t += 30000;
    await s.page.evaluate(T => { window.__now = T; }, t);
    // the note open in the editor, then its "Mais ações" menu → "Renomear" (renomearNota), the
    // app's own dialog (UD.prompt) and the name typed by a person; back to the city
    await s.page.evaluate(path => UrbeCore.commands.execute('document.open', { path }), step.from);
    await s.page.click('#editorFull [aria-label="Mais ações"]');
    await s.page.click('#explorerSheetActions button.explorerAction:has-text("Renomear")');
    await s.page.waitForSelector('.udlg input', { timeout: 10000 });
    await s.page.fill('.udlg input', step.typed);
    await s.page.click('.udlg [data-primary]');
    await settle(s.page);
    steps[step.key] = { ...step, ...(await record(s.page)) };
    if (steps[step.key].files[step.from] !== undefined && step.key !== 'case') throw new Error(step.key + ': the old file is still there');
  }
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
