// UC-18 oracle: "Localizar e substituir" of the UNMODIFIED Urbe 1.8.4-beta (src/editor/workspace.js find/replace, the
// functions find-ui.js calls): hits (index, length, line) and the text after Substituir (first) and Todos, for a set of
// texts and queries, recorded from the real app in Chromium. Test-only tool.
//
//   PLAYWRIGHT_MODULE=/opt/node22/lib/node_modules/playwright/index.mjs \
//   node csharp/tools/legacy-find-oracle.mjs > csharp/tests/fixtures/editor-find.json
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

const notes = { 'Nota.md': '# Nota\n\n[[Outra]]\n', 'Outra.md': '# Outra\n' };
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


const cases = [
  ['Casa casa CASA\ncasinha', 'casa', 'lar'],
  ['aaaa', 'aa', 'b'],
  ['Ação ação AÇÃO', 'ação', 'x'],
  ['linha 1\nlinha 2\n\nlinha 4 fim', 'fim', ''],
  ['nada aqui', 'zzz', 'y'],
  ['[[Plano]] e [[plano]]', '[[plano]]', '[[Projeto]]'],
  ['a.b*c a.b*c', '.b*', '+'],
  ['', 'x', 'y'],
  ['texto', '', 'y'],
  ['😀 emoji 😀 fim', '😀', ':)'],
  [Array.from({ length: 1205 }, () => 'x').join(' '), 'x', 'y']
];
try {
  const s = await boot({ ...notes, '.urbe/tutorial.json': tutorialMarker }, 2);
  const out = [];
  for (const [text, query, repl] of cases) {
    out.push(await s.page.evaluate(([text, query, repl]) => {
      const docs = UrbeCore.service('documents'), ws = UrbeCore.service('editor.workspace');
      const n = (window.__case = (window.__case || 0) + 1);
      const d = docs.upsert({ path: 'Caso ' + n + '.md', content: text }, { source: 'oracle' });
      const hits = ws.find(query, d.id);
      const one = ws.replace(query, repl, d.id, { all: false });
      const afterOne = one ? one.content : null;
      const e = docs.upsert({ path: 'Caso ' + n + ' todos.md', content: text }, { source: 'oracle' });
      const all = ws.replace(query, repl, e.id, { all: true });
      const afterAll = all ? all.content : null;
      return { text, query, replacement: repl, hits, afterOne, afterAll };
    }, [text, query, repl]));
  }
  await s.context.close();
  process.stdout.write(JSON.stringify({ source: 'apps/urbe 1.8.4-beta src/editor/workspace.js', cases: out }) + '\n');
} finally {
  await browser.close();
  server.close();
}
