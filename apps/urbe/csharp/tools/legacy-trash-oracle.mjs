// UC-19 oracle: "Excluir" of a note house in the UNMODIFIED Urbe 1.8.4-beta. The house panel's Excluir
// (deleteBtn → removerNota) and the editor's "Mais ações" → "Mover para a lixeira" ask UD.confirm, then
// explorer.delete → trash.trash(id) (src/core/trash.js: the document kept in the trash, removed from the
// store) and the house leaves the city; workspace.js flush writes .urbe/trash.v2.json and removes the file.
// The oracle records every file of the vault (the trash and the map included) after each deletion.
// Math.random is a fixed LCG; Date.now() and new Date() read window.__now (as legacy-mapa-oracle.mjs).
//   steps: "panel"  Projetos/Tarefas.md through the house panel
//          "editor" Área/Leituras.md through the editor menu
//   then, in Notas → Mais opções → Lixeira (src/explorer/mobile-ui.js renderTrash):
//          "restore" Restaurar Tarefas (trash.restore: the document back at its path, a house for it)
//          "purge"   ✕ Excluir de vez Leituras, confirmed (trash.purge)
// Test-only tool: never part of the native app.
//
//   PLAYWRIGHT_MODULE=/opt/node22/lib/node_modules/playwright/index.mjs \
//   node csharp/tools/legacy-trash-oracle.mjs > csharp/tests/fixtures/legacy-city/trash-note-run-a.json
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
  state.files = Object.fromEntries(Object.keys(files).filter(p => !p.startsWith('.urbe/'))
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

try {
  const files = { ...notes, '.urbe/tutorial.json': JSON.stringify({ versao: 'oracle', em: '2025-10-09T00:00:00.000Z' }) };
  const s = await boot(files, Object.keys(notes).length);
  const open = await record(s.page);
  const steps = {};

  // 1. the house panel: tap the house, Excluir, confirm
  await s.page.evaluate(T => { window.__now = T; }, T0 + 30000);
  const src = open.buildings.find(b => b.path === 'Projetos/Tarefas.md');
  const hp = await tilePoint(s.page, src.x + 1, src.y + 1);
  await s.page.mouse.click(hp.x, hp.y);
  await s.page.waitForSelector('#housePanel.open', { timeout: 10000 });
  await s.page.click('#deleteBtn');
  await s.page.waitForSelector('.udlg [data-primary]', { timeout: 10000 });
  const panelDialog = await s.page.evaluate(() => document.querySelector('.udlg').innerText);
  await s.page.click('.udlg [data-primary]');
  await settle(s.page);
  steps.panel = { from: 'Projetos/Tarefas.md', dialog: panelDialog, ...(await record(s.page)) };

  // 2. the editor: open the note, Mais ações → Mover para a lixeira, confirm
  await s.page.evaluate(T => { window.__now = T; }, T0 + 60000);
  await s.page.evaluate(path => UrbeCore.commands.execute('document.open', { path }), 'Área/Leituras.md');
  await s.page.click('#editorFull [aria-label="Mais ações"]');
  await s.page.click('#explorerSheetActions button.explorerAction:has-text("Mover para a lixeira")');
  await s.page.waitForSelector('.udlg [data-primary]', { timeout: 10000 });
  await s.page.click('.udlg [data-primary]');
  await settle(s.page);
  steps.editor = { from: 'Área/Leituras.md', ...(await record(s.page)) };
  const after = {};

  // 3. Notas → Mais opções → Lixeira → Restaurar (Tarefas)
  await s.page.evaluate(T => { window.__now = T; }, T0 + 90000);
  await s.page.evaluate(() => UrbeCore.commands.execute('ui.explorer.open'));
  await s.page.click('[data-more]');
  await s.page.locator('.udlg button', { hasText: 'Lixeira' }).first().click();
  await s.page.waitForSelector('[data-restore]', { timeout: 10000 });
  await s.page.evaluate(() => {
    const it = UrbeCore.service('trash').list().find(x => x.originalPath === 'Projetos/Tarefas.md');
    document.querySelector('[data-restore="' + it.document.id + '"]').click();
  });
  await settle(s.page);
  after.restore = { path: 'Projetos/Tarefas.md', ...(await record(s.page)) };

  // 4. ✕ Excluir de vez (Leituras), confirmed
  await s.page.evaluate(T => { window.__now = T; }, T0 + 120000);
  await s.page.evaluate(() => {
    const it = UrbeCore.service('trash').list().find(x => x.originalPath === 'Área/Leituras.md');
    document.querySelector('[data-purge="' + it.document.id + '"]').click();
  });
  await s.page.waitForSelector('.udlg [data-primary]', { timeout: 10000 });
  const purgeDialog = await s.page.evaluate(() => document.querySelector('.udlg').innerText);
  await s.page.click('.udlg [data-primary]');
  await settle(s.page);
  after.purge = { path: 'Área/Leituras.md', dialog: purgeDialog, ...(await record(s.page)) };
  await s.context.close();
  if (after.restore.files['Projetos/Tarefas.md'] === undefined) throw new Error('restore: no file');
  for (const k of Object.keys(steps)) if (steps[k].files[steps[k].from] !== undefined || !steps[k].trashWritten) throw new Error(k + ': not deleted');
  process.stdout.write(JSON.stringify({
    source: 'apps/urbe 1.8.4-beta, vault "Urbe" in IndexedDB, Math.random LCG(0x12345678), Date.now/new Date = window.__now',
    files: Object.fromEntries(Object.keys(notes).sort((x, y) => (x < y ? -1 : x > y ? 1 : 0)).map(p => [p, notes[p]])),
    open, steps, after
  }) + '\n');
} finally {
  await browser.close();
  server.close();
}
