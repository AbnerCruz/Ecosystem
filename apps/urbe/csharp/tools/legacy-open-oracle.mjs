// UC-19 oracle: runs the UNMODIFIED Urbe 1.8.4-beta (apps/urbe) in Chromium and records how it
// OPENS A REAL VAULT (app.js abrirCidade chain: urbePersistence.load → v21OpenCity →
// rebuildRoadNetwork → urbeCasasNosBairros/urbeTaparTodos), so the C# port (LegacyCity.Open) can be
// compared tile by tile. Math.random and Date.now are fixed; the ids the original generates are
// recorded so the C# can be fed the same ids.
//
// The vault is seeded in the app's own IndexedDB adapter (src/persistence/adapters/idb.js, store
// "fs", key "<vault>/<path>") before boot; the app opens the vault "Urbe" by itself.
//   phase "fresh": the notes without .urbe/mapa.json (a real vault opened for the first time).
//   phase "mapa":  the mapa.json the app itself produced in "fresh" (persistence.metadataProvider,
//                  i.e. estadoDesejado), with some geometry removed so both the restore and the
//                  create paths of v21OpenCity run, a new folder, a file building and a camera.
// Test-only tool: never part of the native app.
//
//   node csharp/tools/legacy-open-oracle.mjs > csharp/tests/fixtures/legacy-city/open-vault-run-a.json
// Ids depend on how many Math.random calls ran before the open, so each run is a different city
// (run-a/run-b); the C# must reproduce each one from its ids.
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

// The vault: folders, loose notes and [[links]] (paths chosen so localeCompare ≠ ordinal: "Área").
const notes = {
  'Comece.md': '# Comece\n\nVeja [[Plano]] e [[Ideias]].\n',
  'Diário.md': '# Diário\n\nHoje pensei em [[Ideias]].\n',
  'Projetos/Plano.md': '# Plano\n\nPassos em [[Tarefas]]. Volta para [[Comece]].\n',
  'Projetos/Tarefas.md': '# Tarefas\n\n- revisar o [[Plano]]\n',
  'Projetos/Arquivo/Antigo.md': '# Antigo\n\nVeio do [[Plano]].\n',
  'Área/Ideias.md': '# Ideias\n\nLer [[Leituras]].\n',
  'Área/Leituras.md': '# Leituras\n\nNada ainda.\n',
  'Zeta/Solta.md': '# Solta\n\nDe volta ao [[Comece]].\n'
};
const tutorialMarker = JSON.stringify({ versao: 'oracle', em: '2025-10-09T00:00:00.000Z' });

const browser = await chromium.launch(process.env.CHROMIUM_PATH ? { executablePath: process.env.CHROMIUM_PATH } : {});

async function run(files, expectedBuildings) {
  const context = await browser.newContext({ viewport: { width: 1280, height: 760 }, serviceWorkers: 'block' });
  try {
    await context.route(/^https?:\/\/(?!127\.0\.0\.1)/, r => r.abort());
    await context.addInitScript(() => {
      let s = 0x12345678 >>> 0;
      Math.random = () => { s = (Math.imul(s, 1664525) + 1013904223) >>> 0; return s / 4294967296; };
      const T = 1760000000000; Date.now = () => T;
    });
    const page = await context.newPage();
    // same origin, before the app: write the vault with the app's own IndexedDB layout
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
    // urbeEnquadrarNotas (2 frames), scheduleRoadRebuild (120 ms) and the tutorial check (300 ms) settle
    await page.waitForTimeout(2500);
    return await page.evaluate(() => {
      const world = UrbeCore.service('diagnostics.world').legacy();
      const docs = UrbeCore.service('documents');
      const cam = UrbeCore.service('world.life.host').camera();
      const wrap = document.getElementById('wrap');
      return {
        documents: docs.list().map(d => ({ path: d.path, id: d.id })),
        regions: world.regions.map(r => ({ id: r.id, name: r.name, parentId: r.parentId || null, color: r.color,
          x: r.x, y: r.y, w: r.w, h: r.h, cells: r.cells ? r.cells.slice() : null })),
        buildings: world.buildings.map(b => ({ id: b.id, path: b.tipo === 'nota' ? ((docs.get(b.documentId) || {}).path || null) : null,
          tipo: b.tipo, name: b.name, regionId: b.regionId || null, x: b.x, y: b.y, w: b.w, h: b.h, sprite: b.sprite })),
        roads: Array.from(world.roads),
        links: world.links.map(l => [l.from, l.to]),
        camera: { x: cam.x, y: cam.y, z: cam.z },
        view: { w: wrap.clientWidth, h: wrap.clientHeight },
        mapa: UrbeCore.service('persistence').metadataProvider()
      };
    });
  } finally {
    await context.close();
  }
}

try {
  const freshFiles = { ...notes, '.urbe/tutorial.json': tutorialMarker };
  const fresh = await run(freshFiles, Object.keys(notes).length);

  // mapa.json as the app saved it, then partly forgotten (as after an old version or a crash)
  const mapa = JSON.parse(JSON.stringify(fresh.mapa));
  mapa.regioes = mapa.regioes.filter(r => r.caminho !== 'Projetos/Arquivo');         // child recreated inside a restored parent
  for (const r of mapa.regioes) {
    if (r.caminho === 'Área') { r.w = 0; }                                             // recreated where its houses are (urbeOrigemPelasCasas)
    if (r.caminho === 'Zeta') { r.cells = null; }                                      // rectangle region (no cells)
  }
  delete mapa.notas['Diário.md'];                                                       // loose note placed again (vagaAleatoria)
  delete mapa.notas['Projetos/Tarefas.md'].x;                                          // placed again in its neighbourhood
  const plano = mapa.notas['Projetos/Plano.md'];
  mapa.construcoes = [{ id: 'ast-oracle-1', parentId: null, tipo: 'arquivo', fileClass: 'image', name: 'foto.png', fileName: 'foto.png',
    caminho: 'Projetos', x: plano.x + 6, y: plano.y, w: 3, h: 3, description: '', sprite: 'file-image', parentNoteName: null,
    files: [], anexos: [], created: '2025-10-09', modified: '2025-10-09' }];
  mapa.camera = { x: fresh.camera.x + 96, y: fresh.camera.y - 64, z: .1 };            // z is clamped to .22
  const mapaFiles = { ...notes, 'Novo/Nota nova.md': '# Nota nova\n\nLiga [[Solta]].\n',
    '.urbe/tutorial.json': tutorialMarker, '.urbe/mapa.json': JSON.stringify(mapa, null, 1) };
  const reopened = await run(mapaFiles, Object.keys(notes).length + 2);

  // mapa.json that breaks the neighbourhood rule (houses moved by hand, a stale shape): the open
  // must run urbeCasasNosBairros (absorb and relocate) and urbeTaparTodos (child → parent, holes)
  const regra = JSON.parse(JSON.stringify(fresh.mapa));
  const reg = c => regra.regioes.find(r => r.caminho === c);
  const projetos = reg('Projetos'), arquivo = reg('Projetos/Arquivo'), area = reg('Área');
  const inP = new Set(projetos.cells), inA = new Set(arquivo.cells);
  const houses = Object.values(regra.notas).map(n => [n.x, n.y]);
  const free = (x, y) => !houses.some(([hx, hy]) => x >= hx - 1 && x < hx + 4 && y >= hy - 1 && y < hy + 5);
  const solid = (x, y) => { for (let dy = -1; dy <= 2; dy++) for (let dx = -1; dx <= 2; dx++) if (!inP.has((x + dx) + ',' + (y + dy)) || inA.has((x + dx) + ',' + (y + dy))) return false; return true; };
  const hole = projetos.cells.map(k => k.split(',').map(Number)).find(([x, y]) => solid(x, y) && free(x, y) && free(x + 1, y + 1));
  if (!hole) throw new Error('no place for a hole in Projetos');
  const holed = new Set([[0, 0], [1, 0], [0, 1], [1, 1]].map(([dx, dy]) => (hole[0] + dx) + ',' + (hole[1] + dy)));
  projetos.cells = projetos.cells.filter(k => !holed.has(k));                          // closed hole → urbeTaparBuracos
  const extra = [];
  for (const k of projetos.cells) {
    const [x, y] = k.split(',').map(Number);
    for (const [dx, dy] of [[1, 0], [0, 1], [-1, 0], [0, -1]]) {
      const n = (x + dx) + ',' + (y + dy);
      if (!inP.has(n) && !extra.includes(n) && extra.length < 5) extra.push(n);
    }
  }
  arquivo.cells = arquivo.cells.concat(extra);                                          // child outside its parent → urbeTaparTodos
  regra.notas['Área/Leituras.md'].x += 20;                                              // far from its neighbourhood → new lot
  regra.notas['Área/Ideias.md'].x = area.x + area.w;                                    // just outside → the neighbourhood absorbs it
  const regraFiles = { ...notes, '.urbe/tutorial.json': tutorialMarker, '.urbe/mapa.json': JSON.stringify(regra, null, 1) };
  const ruled = await run(regraFiles, Object.keys(notes).length);

  const notesOf = f => Object.fromEntries(Object.keys(f).filter(p => !p.startsWith('.urbe/'))
    .sort((x, y) => (x < y ? -1 : x > y ? 1 : 0)).map(p => [p, f[p]]));
  const strip = o => { const { mapa: _m, ...rest } = o; return rest; };
  process.stdout.write(JSON.stringify({
    source: 'apps/urbe 1.8.4-beta, vault "Urbe" opened from IndexedDB, Math.random LCG(0x12345678), Date.now=1760000000000',
    // files: path → content, in the order the IndexedDB adapter lists them (key order)
    fresh: { files: notesOf(freshFiles), ...strip(fresh) },
    mapa: { files: notesOf(mapaFiles), mapaJson: mapaFiles['.urbe/mapa.json'], ...strip(reopened) },
    regra: { files: notesOf(regraFiles), mapaJson: regraFiles['.urbe/mapa.json'], ...strip(ruled) }
  }) + '\n');
} finally {
  await browser.close();
  server.close();
}
