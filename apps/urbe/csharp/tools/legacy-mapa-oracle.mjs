// UC-19 oracle: runs the UNMODIFIED Urbe 1.8.4-beta (apps/urbe) in Chromium and records the exact
// text of .urbe/mapa.json that the app WRITES (src/persistence/workspace.js flush, the single writer
// of the map, REQ-040), read back from the app's own IndexedDB storage after the debounce
// (marcarSinc 120 ms → schedule 900 ms → flush). The map content is the last estadoDesejado chain of
// app.js (base ≈2493 → v22 "salvo estável" ≈2640 → urbeLegacyDesired ≈3655) through
// urbePersistence.metadataProvider (≈3673). The C# port (Urbe.Core LegacyCityMap) must produce the
// same bytes from the same city.
//
// Math.random is a fixed LCG; Date.now() and new Date() read window.__now, which the oracle moves
// forward between steps (so "salvo" and nowDate() are known). The ids the original generates
// (id('r'), id('b'), doc ids from crypto.randomUUID) are recorded so the C# can be fed the same ids.
//
//   phase "fresh":   a vault with notes and no .urbe/mapa.json; the first save writes the map.
//   phase "session": the same vault reopened with a map (as the app wrote it, then edited like an
//                    older/other install: a region recreated, a rectangle region, notes without a
//                    place, a new folder, tags/aiLocal/anexos, a file building with an invalid id,
//                    a float camera). Then, in the same session:
//                      1. open                         → mapa written (record "open")
//                      2. clock +30 s, a note edited without changing tags → save runs, map
//                         unchanged so "salvo" is kept and nothing is rewritten (record "edit")
//                      3. clock +60 s, camera panned/zoomed only → no save is scheduled (record "pan")
//                      4. a note edited adding a #tag → save: new camera, new tags, new "salvo"
//                         (record "tag")
//                      5. clock +120 s, "Nova nota" through the app's own + button (v23Fab →
//                         v23MenuCriar → v21CreateFileInRegion(null)), name typed in the dialog
//                         → save (record "create")
// Test-only tool: never part of the native app.
//
//   node csharp/tools/legacy-mapa-oracle.mjs > csharp/tests/fixtures/legacy-city/save-mapa-run-a.json
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
  'Projetos/Arquivo/Antigo.md': '# Antigo\n\nVeio do [[Plano]].\n',
  'Área/Ideias.md': '# Ideias\n\nLer [[Leituras]]. "aspas" \\ barra\ttab\n',
  'Área/Leituras.md': '# Leituras\n\nNada ainda.\n',
  'Zeta/Solta.md': '# Solta\n\nDe volta ao [[Comece]].\n'
};
const tutorialMarker = JSON.stringify({ versao: 'oracle', em: '2025-10-09T00:00:00.000Z' });
const T0 = 1760000000000;
const MAPA = '.urbe/mapa.json';

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

// marcarSinc (120 ms) + schedule (900 ms) + flush, framing and road rebuilds: wait until nothing is pending
async function settle(page) {
  await page.waitForTimeout(2500);
  await page.waitForFunction(() => {
    const p = UrbeCore.service('persistence');
    return !p.timer && !p.busy && !p.pending;
  }, null, { timeout: 30000 });
  await page.waitForTimeout(300);
}

async function readMapa(page) {
  return page.evaluate(async key => {
    const db = await new Promise((ok, err) => { const r = indexedDB.open('knowledge-city'); r.onsuccess = () => ok(r.result); r.onerror = () => err(r.error); });
    const v = await new Promise((ok, err) => { const t = db.transaction('fs', 'readonly'), q = t.objectStore('fs').get(key); q.onsuccess = () => ok(q.result); q.onerror = () => err(q.error); });
    db.close();
    return v === undefined ? null : v;
  }, 'Urbe/' + MAPA);
}

// the city as the C# needs it to be rebuilt (same shape as legacy-open-oracle.mjs) + the written map
async function record(page) {
  const state = await page.evaluate(() => {
    const world = UrbeCore.service('diagnostics.world').legacy();
    const docs = UrbeCore.service('documents');
    const p = UrbeCore.service('persistence');
    return {
      now: Date.now(),
      documents: docs.list().map(d => ({ path: d.path, id: d.id, content: d.content })),
      regions: world.regions.map(r => ({ id: r.id, uid: r.uid || null, name: r.name, parentId: r.parentId || null, color: r.color,
        x: r.x, y: r.y, w: r.w, h: r.h, cells: r.cells ? r.cells.slice() : null })),
      buildings: world.buildings.map(b => ({ id: b.id, path: b.tipo === 'nota' ? ((docs.get(b.documentId) || {}).path || null) : null,
        tipo: b.tipo, name: b.name, regionId: b.regionId || null, x: b.x, y: b.y, w: b.w, h: b.h, sprite: b.sprite })),
      roads: Array.from(world.roads),
      camera: (c => ({ x: c.x, y: c.y, z: c.z }))(UrbeCore.service('world.life.host').camera()),
      saveState: p.state
    };
  });
  state.mapaWritten = await readMapa(page);
  return state;
}

async function editNote(page, path, content) {
  await page.evaluate(([path, content]) => {
    const docs = UrbeCore.service('documents'), d = docs.get(path);
    UrbeCore.commands.execute('document.update', { ...d, content, source: 'oracle.edit' });
  }, [path, content]);
}

try {
  // ---------------------------------------------------------------- fresh
  const freshFiles = { ...notes, '.urbe/tutorial.json': tutorialMarker };
  const f = await boot(freshFiles, Object.keys(notes).length);
  const fresh = await record(f.page);
  await f.context.close();
  if (!fresh.mapaWritten) throw new Error('fresh: the app did not write .urbe/mapa.json');

  // ---------------------------------------------------------------- session
  const mapa = JSON.parse(fresh.mapaWritten);
  mapa.regioes = mapa.regioes.filter(r => r.caminho !== 'Projetos/Arquivo');          // recreated: regionFor id
  for (const r of mapa.regioes) {
    if (r.caminho === 'Área') { r.w = 0; r.descricao = 'Ideias soltas'; }              // recreated where its houses are, keeps its id
    if (r.caminho === 'Zeta') { r.cells = null; }                                       // rectangle region (no cells)
    if (r.caminho === 'Projetos') { r.descricao = 'Trabalho "sério"\ncom quebra'; r.id = 'reg_invalido-x'; }
  }
  delete mapa.notas['Diário.md'];                                                        // loose note placed again
  delete mapa.notas['Projetos/Tarefas.md'].x;                                           // placed again in its neighbourhood
  const plano = mapa.notas['Projetos/Plano.md'];
  plano.tags = ['manual', 'Ação'];                                                       // tags kept from the map
  plano.aiLocal = { instructions: 'seja breve', memory: '', artifacts: [], extra: { b: 1, 10: 'dez', 2: 'dois', a: -0.5 } };
  plano.anexos = [{ nome: 'ref.pdf', relPath: 'Projetos/ref.pdf', tamanho: 12, dataUrl: 'data:x', mime: 'application/pdf' }];
  plano.criado = '2024-01-02'; plano.modificado = '2024-03-04';
  mapa.notas['Comece.md'].criado = '2023-05-06';
  mapa.construcoes = [{ id: 'ast-oracle-1', parentId: null, tipo: 'arquivo', fileClass: 'image', name: 'foto.png', fileName: 'foto.png',
    caminho: 'Projetos', x: plano.x + 6, y: plano.y, w: 3, h: 3, description: '', sprite: 'file-image', parentNoteName: 'Plano',
    files: [{ nome: 'foto.png', tipo: 'png', mime: 'image/png', relPath: 'Projetos/foto.png', folderPath: 'Projetos', tamanho: 3, _blob: 'x' }],
    anexos: [], created: '2025-10-01', modified: '2025-10-02' }];
  mapa.camera = { x: fresh.camera.x + 96.25, y: fresh.camera.y - 64.125, z: 1.0000001 };
  const sessionFiles = { ...notes, 'Novo/Nota nova.md': '# Nota nova\n\nLiga [[Solta]].\n',
    '.urbe/tutorial.json': tutorialMarker, [MAPA]: JSON.stringify(mapa, null, 1) };
  const s = await boot(sessionFiles, Object.keys(notes).length + 2);
  const steps = {};
  steps.open = await record(s.page);

  await s.page.evaluate(T => { window.__now = T; }, T0 + 30000);
  await editNote(s.page, 'Área/Leituras.md', '# Leituras\n\nNada ainda. Talvez amanhã.\n');
  await settle(s.page);
  steps.edit = await record(s.page);

  await s.page.evaluate(T => {
    window.__now = T;
    // what the pan/pinch handlers do (v21CanvasMove mutates camera.x/y/z); nothing else is called
    const c = UrbeCore.service('world.life.host').camera();
    c.x = c.x + 123.4567890123; c.y = c.y - 0.1; c.z = 1.37;
  }, T0 + 60000);
  await settle(s.page);
  steps.pan = await record(s.page);

  await editNote(s.page, 'Comece.md', '# Comece\n\nVeja [[Plano]] e [[Ideias]]. #novo #ação\n');
  await settle(s.page);
  steps.tag = await record(s.page);

  await s.page.evaluate(T => { window.__now = T; }, T0 + 120000);
  // the + button (v23Fab → v23MenuCriar → "Nova nota" → v21CreateFileInRegion(null)), name typed in the dialog
  await s.page.click('#v23Fab');
  await s.page.click('#explorerSheetActions button:has-text("Nova nota")');
  await s.page.waitForSelector('.udlg input', { timeout: 10000 });
  await s.page.fill('.udlg input', 'Ideia nova');
  await s.page.click('.udlg [data-primary]');
  await s.page.waitForFunction(() => UrbeCore.service('documents').get('Ideia nova.md'), null, { timeout: 10000 });
  await settle(s.page);
  steps.create = await record(s.page);
  steps.create.typed = 'Ideia nova';
  await s.context.close();

  const notesOf = fs => Object.fromEntries(Object.keys(fs).filter(p => !p.startsWith('.urbe/'))
    .sort((x, y) => (x < y ? -1 : x > y ? 1 : 0)).map(p => [p, fs[p]]));
  process.stdout.write(JSON.stringify({
    source: 'apps/urbe 1.8.4-beta, vault "Urbe" in IndexedDB, Math.random LCG(0x12345678), Date.now/new Date = window.__now',
    fresh: { files: notesOf(freshFiles), ...fresh },
    session: { files: notesOf(sessionFiles), mapaJson: sessionFiles[MAPA], steps }
  }) + '\n');
} finally {
  await browser.close();
  server.close();
}
